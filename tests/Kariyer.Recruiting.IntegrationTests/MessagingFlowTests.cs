using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Features.Messaging;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.IntegrationTests;

/// <summary>
/// "Adaylarla iletişime geç": who a message reaches, that it is recorded and announced once, and
/// that the portal can see afterwards who already received one.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MessagingFlowTests(RecruitingDatabase database) : IAsyncLifetime
{
    private const string Company = "c-msg";
    private const string OtherCompany = "c-msg-other";
    private const string Job = "j-msg";
    private const string OtherJob = "j-msg-other";

    private readonly RecordingPublisher published = new();

    public async Task InitializeAsync() => await database.ExecuteAsync(
        $"""
         INSERT INTO public.company (uid, external_id, company_name) VALUES
           ('{Company}', '00000000-0000-0000-0000-000000000010', 'PSB Teknoloji'),
           ('{OtherCompany}', '00000000-0000-0000-0000-000000000011', 'Başka Şirket')
         ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.employee (uid, name, surname, email) VALUES
           ('e-msg-1', 'Ayşe', 'Kaya', 'ayse.kaya@example.com'),
           ('e-msg-2', 'Mert', 'Demir', 'mert@example.com'),
           ('e-msg-3', 'Ece', 'Yıldız', NULL)
         ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.company_job (uid, company_uid, title, department) VALUES
           ('{Job}', '{Company}', 'Backend Developer', 'Yazılım'),
           ('{OtherJob}', '{OtherCompany}', 'Muhasebe', 'Finans')
         ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.job_application (uid, job_uid, applicant_uid, application_status, applied_at) VALUES
           ('a-msg-1', '{Job}', 'e-msg-1', 'pending', now()),
           ('a-msg-2', '{Job}', 'e-msg-2', 'under_review', now()),
           ('a-msg-3', '{Job}', 'e-msg-3', 'pending', now()),
           ('a-msg-other', '{OtherJob}', 'e-msg-1', 'pending', now())
         ON CONFLICT (uid) DO NOTHING;
         """);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_audience_is_the_postings_applicants_in_the_chosen_stages()
    {
        await using RecruitingDbContext db = database.CreateContext();

        IReadOnlyList<MessageAudienceRow> fresh =
            await new ApplicationReadStore(db).AudienceAsync(Job, Company, ["NEW"], null, Cancellation);

        Assert.Equal(["a-msg-1", "a-msg-3"], fresh.Select(r => r.ApplicationUid).Order());
        Assert.DoesNotContain(fresh, r => r.ApplicationUid == "a-msg-other");

        IReadOnlyList<MessageAudienceRow> nobody =
            await new ApplicationReadStore(db).AudienceAsync(Job, OtherCompany, null, null, Cancellation);

        Assert.Empty(nobody);
    }

    [Fact]
    public async Task A_message_is_recorded_announced_once_and_flags_its_recipients()
    {
        await using RecruitingDbContext db = database.CreateContext();

        IResult result = await Handler(db).HandleAsync(
            Job,
            new SendCandidateMessageRequest(["a-msg-1", "a-msg-2", "a-msg-3", "a-msg-other"], "Süreç hakkında", "Merhaba, kısa bir güncelleme…"),
            new CompanyContext(Company, "u-1", "Polat Kaya"),
            Cancellation);

        SendCandidateMessageResponse body = Assert.IsType<SendCandidateMessageResponse>(ValueOf(result));

        Assert.Equal(["a-msg-1", "a-msg-2"], body.Sent.Select(r => r.ApplicationUid).Order());
        Assert.Contains(body.Skipped, s => s.ApplicationUid == "a-msg-3" && s.Reason == SendCandidateMessageHandler.NoEmail);
        Assert.Contains(body.Skipped, s => s.ApplicationUid == "a-msg-other" && s.Reason == SendCandidateMessageHandler.NotFound);

        CandidatesMessagedEvent sent = Assert.IsType<CandidatesMessagedEvent>(Assert.Single(published.Messages));

        Assert.Equal(body.MessageUid, sent.MessageId);
        Assert.Equal("Backend Developer", sent.JobTitle);
        Assert.Equal("PSB Teknoloji", sent.CompanyName);
        Assert.Equal("Polat Kaya", sent.SenderName);
        Assert.Equal(["ayse.kaya@example.com", "mert@example.com"], sent.Recipients.Select(r => r.Email).Order());

        await using RecruitingDbContext verify = database.CreateContext();

        Assert.Equal(1, await verify.Messages.CountAsync(m => m.Uid == body.MessageUid, Cancellation));
        List<string> entries = await verify.Activity
            .Where(a => a.Type == ActivityType.MessageSent && a.JobUid == Job)
            .Select(a => a.Metadata)
            .ToListAsync(Cancellation);

        Assert.Equal(2, entries.Count(metadata => metadata.Contains(body.MessageUid, StringComparison.Ordinal)));

        // The portal's warning: these two now read as already messaged, the third does not.
        IReadOnlyList<MessageAudienceRow> after =
            await new ApplicationReadStore(verify).AudienceAsync(Job, Company, null, null, Cancellation);

        Assert.NotNull(after.Single(r => r.ApplicationUid == "a-msg-1").LastMessagedAt);
        Assert.Null(after.Single(r => r.ApplicationUid == "a-msg-3").LastMessagedAt);
    }

    [Fact]
    public async Task An_empty_message_or_an_unreachable_selection_sends_nothing()
    {
        await using RecruitingDbContext db = database.CreateContext();
        CompanyContext company = new(Company, "u-1", "Polat Kaya");

        IResult empty = await Handler(db).HandleAsync(Job, new SendCandidateMessageRequest(["a-msg-1"], null, "   "), company, Cancellation);
        IResult unreachable = await Handler(db).HandleAsync(Job, new SendCandidateMessageRequest(["a-msg-3"], null, "Merhaba"), company, Cancellation);

        Assert.Equal(400, StatusOf(empty));
        Assert.Equal(400, StatusOf(unreachable));
        Assert.Empty(published.Messages);
    }

    private SendCandidateMessageHandler Handler(RecruitingDbContext db) => new(
        new ApplicationReadStore(db),
        new CandidateMessageRepository(db),
        new ActivityWriter(db),
        published,
        new CompanyDirectory(db, new DisabledCacheStore(), Microsoft.Extensions.Options.Options.Create(new Api.Common.Configuration.GarnetOptions { Enabled = false })),
        new UnitOfWork(db),
        TimeProvider.System);

    private static CancellationToken Cancellation => CancellationToken.None;

    private static object? ValueOf(IResult result) => result.GetType().GetProperty("Value")?.GetValue(result);

    private static int StatusOf(IResult result) =>
        result.GetType().GetProperty("StatusCode")?.GetValue(result) as int? ?? 200;

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
