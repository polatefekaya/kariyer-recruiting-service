using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Features.Applications.ChangeStage;
using Kariyer.Recruiting.Api.Features.Applications.SaveNote;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public sealed class PipelineFlowTests(RecruitingDatabase database) : IAsyncLifetime
{
    private const string Company = "c-flow";
    private const string Job = "j-flow";
    private const string Candidate = "e-flow";
    private const string Application = "a-flow";

    /// <summary>
    /// One application per test. They share a database, so a test that moves the fixture row
    /// would decide what the next test sees.
    /// </summary>
    private async Task<string> NewApplicationAsync(string key, string legacyStatus = "pending")
    {
        string uid = $"a-flow-{key}";

        await database.ExecuteAsync(
            $"""
             INSERT INTO public.job_application (uid, job_uid, applicant_uid, application_status, applied_at)
             VALUES ('{uid}', '{Job}', '{Candidate}', '{legacyStatus}', now())
             ON CONFLICT (uid) DO NOTHING;
             """);

        return uid;
    }

    public async Task InitializeAsync() => await database.ExecuteAsync(
        $"""
         INSERT INTO public.company (uid, external_id, company_name)
         VALUES ('{Company}', '00000000-0000-0000-0000-000000000002', 'PSB Teknoloji') ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.employee (uid, name, surname, email)
         VALUES ('{Candidate}', 'Ayşe', 'Şimşek', 'ayse@example.com') ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.company_job (uid, company_uid, title, department)
         VALUES ('{Job}', '{Company}', 'Frontend Developer', 'Yazılım') ON CONFLICT (uid) DO NOTHING;

         INSERT INTO public.job_application (uid, job_uid, applicant_uid, application_status, applied_at)
         VALUES ('{Application}', '{Job}', '{Candidate}', 'pending', now()) ON CONFLICT (uid) DO NOTHING;
         """);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_application_with_no_pipeline_row_reads_as_its_legacy_stage()
    {
        string application = await NewApplicationAsync("legacy", "under_review");

        ApplicationSummary? summary = await ReadStore().FindAsync(application, Company, Cancellation);

        Assert.NotNull(summary);
        Assert.Equal(ApplicationStage.Reviewing, summary.Stage);
        Assert.Equal("Frontend Developer", summary.JobTitle);
    }

    [Fact]
    public async Task Moving_a_stage_writes_the_pipeline_the_activity_and_leaves_the_legacy_column_alone()
    {
        string application = await NewApplicationAsync("move");

        await using RecruitingDbContext db = database.CreateContext();

        IResult result = await Handler(db).HandleAsync(
            application,
            new ChangeStageRequest(ApplicationStage.Reviewing, "İlk değerlendirme"),
            new CompanyContext(Company, "u-1", "Polat Kaya"),
            Cancellation);

        Assert.Equal(200, StatusOf(result));

        await using RecruitingDbContext verify = database.CreateContext();

        Assert.Equal(
            ApplicationStage.Reviewing,
            await verify.Pipelines.Where(p => p.ApplicationUid == application)
                .Select(p => p.Stage)
                .SingleAsync(Cancellation));

        Assert.True(await verify.Activity.AnyAsync(
            a => a.ApplicationUid == application && a.Type == Domain.Activity.ActivityType.StageChanged,
            Cancellation));

        // The Node-owned column is untouched: this service never writes outside its schema.
        Assert.Equal(
            "pending",
            await database.ScalarAsync<string>(
                $"SELECT application_status FROM public.job_application WHERE uid = '{application}'"));
    }

    [Fact]
    public async Task A_second_move_persists_too()
    {
        string application = await NewApplicationAsync("second");

        // The first move creates the pipeline row; the second loads and mutates it. With the
        // context's NoTracking default, that second write is silently dropped unless the
        // aggregate is loaded tracked.
        CompanyContext actor = new(Company, "u-1", "Polat Kaya");

        await using (RecruitingDbContext db = database.CreateContext())
        {
            await Handler(db).HandleAsync(
                application, new ChangeStageRequest(ApplicationStage.Reviewing, null), actor, Cancellation);
        }

        await using (RecruitingDbContext db = database.CreateContext())
        {
            IResult result = await Handler(db).HandleAsync(
                application, new ChangeStageRequest(ApplicationStage.Contact, null), actor, Cancellation);

            Assert.Equal(200, StatusOf(result));
        }

        await using RecruitingDbContext verify = database.CreateContext();

        Assert.Equal(
            ApplicationStage.Contact,
            await verify.Pipelines.Where(p => p.ApplicationUid == application)
                .Select(p => p.Stage)
                .SingleAsync(Cancellation));
    }

    [Fact]
    public async Task An_illegal_move_is_rejected_with_409()
    {
        string application = await NewApplicationAsync("illegal");

        await using RecruitingDbContext db = database.CreateContext();

        IResult result = await Handler(db).HandleAsync(
            application,
            new ChangeStageRequest(ApplicationStage.Hired, null),
            new CompanyContext(Company, "u-1", "Polat Kaya"),
            Cancellation);

        Assert.Equal(409, StatusOf(result));
    }

    [Fact]
    public async Task A_note_is_upserted_then_cleared()
    {
        string application = await NewApplicationAsync("note");

        await using RecruitingDbContext db = database.CreateContext();
        CompanyContext actor = new(Company, "u-1", "Polat Kaya");

        SaveNoteHandler handler = new(
            new ApplicationReadStore(db),
            new ApplicationNoteRepository(db),
            new ActivityWriter(db),
            Invalidator(),
            new UnitOfWork(db),
            TimeProvider.System);

        await handler.HandleAsync(application, new SaveNoteRequest("Maaş beklentisi bant içinde."), actor, Cancellation);

        await using (RecruitingDbContext verify = database.CreateContext())
        {
            Assert.Equal(
                "Maaş beklentisi bant içinde.",
                await verify.Notes.Where(n => n.ApplicationUid == application)
                    .Select(n => n.Body)
                    .SingleAsync(Cancellation));
        }

        await handler.HandleAsync(application, new SaveNoteRequest("   "), actor, Cancellation);

        await using (RecruitingDbContext verify = database.CreateContext())
        {
            Assert.False(await verify.Notes.AnyAsync(n => n.ApplicationUid == application, Cancellation));
        }
    }

    [Fact]
    public async Task The_list_finds_a_candidate_through_the_turkish_fold()
    {
        ApplicationPage page = await ReadStore().ListAsync(
            new ApplicationListQuery { JobUid = Job, CompanyUid = Company, Search = "simsek" },
            Cancellation);

        Assert.Contains(page.Items, item => item.CandidateUid == Candidate);
    }

    [Fact]
    public async Task The_company_wide_list_spans_jobs_and_filters_by_candidate()
    {
        ApplicationPage page = await ReadStore().ListAsync(
            new ApplicationListQuery { CompanyUid = Company, CandidateUid = Candidate },
            Cancellation);

        Assert.All(page.Items, item => Assert.Equal(Candidate, item.CandidateUid));
        Assert.All(page.Items, item => Assert.Equal("Frontend Developer", item.JobTitle));
    }

    [Fact]
    public async Task Stats_count_every_application_under_its_stage()
    {
        IReadOnlyDictionary<string, int> stats = await ReadStore().StatsAsync(Job, Company, Cancellation);

        Assert.True(stats["ALL"] >= 1);
        Assert.Equal(stats["ALL"], stats.Where(pair => pair.Key != "ALL").Sum(pair => pair.Value));
    }

    [Fact]
    public async Task An_interview_can_be_stored_with_its_participants()
    {
        await using RecruitingDbContext db = database.CreateContext();

        Interview interview = Interview.Schedule(
            "i-flow", Application, Job, Company, Candidate,
            new InterviewDraft
            {
                Type = InterviewType.Video,
                StartsAt = DateTimeOffset.UtcNow.AddDays(1),
                DurationMinutes = 45,
                TimeZone = "Europe/Istanbul",
                VideoUrl = "https://meet.google.com/kz-abc-def",
                InterviewerUid = "u-2",
                Participants = [new InterviewParticipantDraft("selin@example.com", InterviewParticipantRole.Interviewer, "Selin")],
            },
            "u-1",
            "İlk tur",
            DateTimeOffset.UtcNow);

        db.Interviews.Add(interview);
        await db.SaveChangesAsync(Cancellation);

        await using RecruitingDbContext verify = database.CreateContext();

        Interview stored = await verify.Interviews
            .Include(i => i.Participants)
            .SingleAsync(i => i.Uid == "i-flow", Cancellation);

        Assert.Equal("u-2", stored.InterviewerUid);
        Assert.Equal("u-1", stored.CreatedBy);
        Assert.Single(stored.Participants);
        Assert.Equal(InterviewConfirmation.Pending, stored.ConfirmationStatus);
    }

    private static CancellationToken Cancellation => CancellationToken.None;

    private ApplicationReadStore ReadStore() => new(database.CreateContext());

    private ChangeStageHandler Handler(RecruitingDbContext db) => new(
        new ApplicationReadStore(db),
        new ApplicationPipelineRepository(db),
        new ActivityWriter(db),
        new NoOpPublisher(),
        new CompanyDirectory(db, new DisabledCacheStore(), GarnetOptions()),
        Invalidator(),
        new UnitOfWork(db),
        new Api.Common.Telemetry.RecruitingMetrics(new DummyMeterFactory()),
        TimeProvider.System);

    private static CacheInvalidator Invalidator() => new(new DisabledCacheStore(), GarnetOptions());

    private static IOptions<GarnetOptions> GarnetOptions() =>
        Microsoft.Extensions.Options.Options.Create(new GarnetOptions { Enabled = false });

    private static int StatusOf(IResult result) =>
        result.GetType().GetProperty("StatusCode")?.GetValue(result) as int? ?? 200;

    private sealed class NoOpPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class =>
            Task.CompletedTask;
    }

    private sealed class DummyMeterFactory : IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new(options);

        public void Dispose()
        {
        }
    }
}
