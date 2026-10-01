using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.IntegrationTests;

/// <summary>
/// The candidate's one-click answer. These links travel by e-mail with no session behind them, so
/// the tests care about what an unsigned, tampered or stale link is allowed to change: nothing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class InterviewConfirmationTests(RecruitingDatabase database) : IAsyncLifetime
{
    private const string Company = "c-confirm";
    private const string Job = "j-confirm";
    private const string Candidate = "e-confirm";

    // xUnit builds a fresh instance per test, so this records one test's events only.
    private readonly RecordingPublisher published = new();

    public async Task InitializeAsync() => await database.ExecuteAsync(
        $"""
         INSERT INTO public.company (uid, external_id, company_name)
         VALUES ('{Company}', '00000000-0000-0000-0000-000000000003', 'PSB Teknoloji') ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.employee (uid, name, surname, email)
         VALUES ('{Candidate}', 'Deniz', 'Yücel', 'deniz@example.com') ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.company_job (uid, company_uid, title, department)
         VALUES ('{Job}', '{Company}', 'Backend Developer', 'Yazılım') ON CONFLICT (uid) DO NOTHING;
         """);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_signed_link_records_the_candidates_answer()
    {
        string interview = await NewInterviewAsync("accept");
        InterviewConfirmationTokens tokens = Tokens();
        string token = tokens.Issue(interview, ConfirmationAnswer.Accept);

        ConfirmationView preview = await Preview(tokens, interview, ConfirmationAnswer.Accept, token);

        Assert.Equal(ConfirmationOutcome.Answerable, preview.Outcome);
        Assert.Equal("Backend Developer", preview.JobTitle);
        Assert.Equal("PSB Teknoloji", preview.CompanyName);

        ConfirmationView answered = await Apply(tokens, interview, ConfirmationAnswer.Accept, token);

        Assert.Equal(ConfirmationOutcome.Answered, answered.Outcome);
        Assert.Equal(InterviewConfirmation.Accepted, await StatusAsync(interview));

        await using RecruitingDbContext verify = database.CreateContext();

        Assert.True(await verify.Activity.AnyAsync(
            a => a.JobUid == Job && a.Type == Domain.Activity.ActivityType.InterviewUpdated, Cancellation));
    }

    [Fact]
    public async Task A_saved_answer_is_announced_to_the_company_side()
    {
        string interview = await NewInterviewAsync("announce");
        InterviewConfirmationTokens tokens = Tokens();

        await Apply(tokens, interview, ConfirmationAnswer.Decline, tokens.Issue(interview, ConfirmationAnswer.Decline));

        InterviewAnsweredEvent answered = Assert.IsType<InterviewAnsweredEvent>(Assert.Single(published.Messages));

        Assert.Equal(InterviewConfirmation.Declined, answered.Answer);
        Assert.Equal(interview, answered.InterviewUid);
        Assert.Equal("Deniz Yücel", answered.CandidateName);
        Assert.Equal("PSB Teknoloji", answered.CompanyName);
        Assert.Equal("Backend Developer", answered.JobTitle);
        Assert.Equal($"https://portal.test/ilanlar/{Job}", answered.CompanyReviewUrl);
    }

    [Fact]
    public async Task Previewing_or_repeating_an_answer_announces_nothing()
    {
        // Mail scanners open links, and a second click is not a new decision — neither may tell
        // the company that the candidate answered again.
        string interview = await NewInterviewAsync("quiet");
        InterviewConfirmationTokens tokens = Tokens();
        string token = tokens.Issue(interview, ConfirmationAnswer.Accept);

        await Preview(tokens, interview, ConfirmationAnswer.Accept, token);
        Assert.Empty(published.Messages);

        await Apply(tokens, interview, ConfirmationAnswer.Accept, token);
        await Apply(tokens, interview, ConfirmationAnswer.Accept, token);

        Assert.Single(published.Messages);
    }

    [Fact]
    public async Task A_second_click_on_the_same_link_is_not_a_new_answer()
    {
        string interview = await NewInterviewAsync("twice");
        InterviewConfirmationTokens tokens = Tokens();
        string token = tokens.Issue(interview, ConfirmationAnswer.Accept);

        await Apply(tokens, interview, ConfirmationAnswer.Accept, token);

        ConfirmationView again = await Apply(tokens, interview, ConfirmationAnswer.Accept, token);

        Assert.Equal(ConfirmationOutcome.AlreadyAnswered, again.Outcome);
    }

    [Fact]
    public async Task The_candidate_can_change_the_answer_while_the_interview_is_still_ahead()
    {
        string interview = await NewInterviewAsync("change");
        InterviewConfirmationTokens tokens = Tokens();

        await Apply(tokens, interview, ConfirmationAnswer.Accept, tokens.Issue(interview, ConfirmationAnswer.Accept));
        await Apply(tokens, interview, ConfirmationAnswer.Decline, tokens.Issue(interview, ConfirmationAnswer.Decline));

        Assert.Equal(InterviewConfirmation.Declined, await StatusAsync(interview));
    }

    /// Opening the link must be safe: mail clients and link scanners follow every URL in a message.
    [Fact]
    public async Task Opening_the_link_does_not_answer_on_the_candidates_behalf()
    {
        string interview = await NewInterviewAsync("preview");
        InterviewConfirmationTokens tokens = Tokens();

        await Preview(tokens, interview, ConfirmationAnswer.Accept, tokens.Issue(interview, ConfirmationAnswer.Accept));

        Assert.Equal(InterviewConfirmation.Pending, await StatusAsync(interview));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("99999999999.aabbcc")]
    public async Task An_unsigned_link_changes_nothing(string token)
    {
        string interview = await NewInterviewAsync($"unsigned-{token.Length}");

        ConfirmationView view = await Apply(Tokens(), interview, ConfirmationAnswer.Accept, token);

        Assert.Equal(ConfirmationOutcome.Invalid, view.Outcome);
        Assert.Equal(InterviewConfirmation.Pending, await StatusAsync(interview));
    }

    /// The answer is signed too, so an accept link cannot be replayed as a decline.
    [Fact]
    public async Task A_token_minted_for_the_other_answer_is_refused()
    {
        string interview = await NewInterviewAsync("swap");
        InterviewConfirmationTokens tokens = Tokens();

        ConfirmationView view = await Apply(
            tokens, interview, ConfirmationAnswer.Decline, tokens.Issue(interview, ConfirmationAnswer.Accept));

        Assert.Equal(ConfirmationOutcome.Invalid, view.Outcome);
        Assert.Equal(InterviewConfirmation.Pending, await StatusAsync(interview));
    }

    [Fact]
    public async Task A_token_minted_for_another_interview_is_refused()
    {
        string interview = await NewInterviewAsync("mine");
        string other = await NewInterviewAsync("theirs");
        InterviewConfirmationTokens tokens = Tokens();

        ConfirmationView view = await Apply(
            tokens, interview, ConfirmationAnswer.Accept, tokens.Issue(other, ConfirmationAnswer.Accept));

        Assert.Equal(ConfirmationOutcome.Invalid, view.Outcome);
        Assert.Equal(InterviewConfirmation.Pending, await StatusAsync(interview));
    }

    [Fact]
    public async Task An_expired_link_is_refused()
    {
        string interview = await NewInterviewAsync("stale");
        string stale = Tokens(new FixedClock(DateTimeOffset.UtcNow.AddDays(-60)))
            .Issue(interview, ConfirmationAnswer.Accept);

        ConfirmationView view = await Apply(Tokens(), interview, ConfirmationAnswer.Accept, stale);

        Assert.Equal(ConfirmationOutcome.Expired, view.Outcome);
        Assert.Equal(InterviewConfirmation.Pending, await StatusAsync(interview));
    }

    [Fact]
    public async Task A_cancelled_interview_can_no_longer_be_answered()
    {
        string interview = await NewInterviewAsync("cancelled");

        await using (RecruitingDbContext db = database.CreateContext())
        {
            Interview stored = await db.Interviews.SingleAsync(i => i.Uid == interview, Cancellation);

            stored.Cancel("u-1", null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Cancellation);
        }

        InterviewConfirmationTokens tokens = Tokens();

        ConfirmationView view = await Apply(
            tokens, interview, ConfirmationAnswer.Accept, tokens.Issue(interview, ConfirmationAnswer.Accept));

        Assert.Equal(ConfirmationOutcome.Closed, view.Outcome);
    }

    private async Task<string> NewInterviewAsync(string key)
    {
        string application = $"a-confirm-{key}";
        string uid = $"i-confirm-{key}";

        await database.ExecuteAsync(
            $"""
             INSERT INTO public.job_application (uid, job_uid, applicant_uid, application_status, applied_at)
             VALUES ('{application}', '{Job}', '{Candidate}', 'pending', now())
             ON CONFLICT (uid) DO NOTHING;
             """);

        await using RecruitingDbContext db = database.CreateContext();

        if (await db.Interviews.AnyAsync(i => i.Uid == uid, Cancellation))
        {
            return uid;
        }

        db.Interviews.Add(Interview.Schedule(
            uid, application, Job, Company, Candidate,
            new InterviewDraft
            {
                Type = InterviewType.Video,
                StartsAt = DateTimeOffset.UtcNow.AddDays(3),
                DurationMinutes = 45,
                TimeZone = "Europe/Istanbul",
                VideoUrl = "https://meet.google.com/kz-abc-def",
                InterviewerUid = "u-2",
            },
            "u-1",
            null,
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(Cancellation);

        return uid;
    }

    private Task<ConfirmationView> Preview(
        InterviewConfirmationTokens tokens, string interview, string answer, string? token) =>
        Run(tokens, handler => handler.PreviewAsync(interview, answer, token, Cancellation));

    private Task<ConfirmationView> Apply(
        InterviewConfirmationTokens tokens, string interview, string answer, string? token) =>
        Run(tokens, handler => handler.ApplyAsync(interview, answer, token, Cancellation));

    private async Task<ConfirmationView> Run(
        InterviewConfirmationTokens tokens, Func<ConfirmInterviewHandler, Task<ConfirmationView>> call)
    {
        await using RecruitingDbContext db = database.CreateContext();

        return await call(new ConfirmInterviewHandler(
            db,
            new InterviewRepository(db),
            new ActivityWriter(db),
            tokens,
            new ApplicationReadStore(db),
            new CompanyDirectory(db, new DisabledCacheStore(), GarnetOptions()),
            published,
            Microsoft.Extensions.Options.Options.Create(new RecruitingOptions { EmployerPortalUrl = "https://portal.test/" }),
            new CacheInvalidator(new DisabledCacheStore(), GarnetOptions()),
            new UnitOfWork(db),
            TimeProvider.System));
    }

    private async Task<string?> StatusAsync(string interview)
    {
        await using RecruitingDbContext db = database.CreateContext();

        return await db.Interviews.Where(i => i.Uid == interview)
            .Select(i => i.ConfirmationStatus)
            .SingleAsync(Cancellation);
    }

    private static InterviewConfirmationTokens Tokens(TimeProvider? clock = null) => new(
        Microsoft.Extensions.Options.Options.Create(new RecruitingOptions { ConfirmationSigningKey = "test-key" }),
        clock ?? TimeProvider.System);

    private static IOptions<GarnetOptions> GarnetOptions() =>
        Microsoft.Extensions.Options.Options.Create(new GarnetOptions { Enabled = false });

    private static CancellationToken Cancellation => CancellationToken.None;

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
