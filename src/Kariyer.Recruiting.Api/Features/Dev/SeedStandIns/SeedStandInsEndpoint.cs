using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Dev.SeedStandIns;

/// <summary>
/// Fills the local stand-ins for the Node-owned tables, so the service recognises real uids while
/// developing against the real backend.
///
/// This is the one place that writes outside the `recruiting` schema, and it exists only because
/// a developer machine has no monolith behind it: the portal reads jobs and applications from the
/// Node API, then hands the same rows here so this service can authorise them. Three independent
/// guards keep it where it belongs — it is not mapped outside Development, the handler re-checks
/// the environment, and it refuses any database that does not carry the stand-in marker table,
/// which only `deploy/local` and `deploy/smoke` create. A real database therefore cannot be
/// written by it even if the service were started in the wrong environment against one.
/// </summary>
public sealed class SeedStandInsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        if (!app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        app.MapPost("dev/stand-ins", HandleAsync)
            .AllowAnonymous()
            .ExcludeFromDescription()
            .WithName("SeedStandIns")
            .WithTags("Dev");
    }

    private static async Task<IResult> HandleAsync(
        StandInSeed seed,
        SeedStandInsHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(seed, cancellationToken);
}

public sealed record StandInCompany(string Uid, string? ExternalId, string? Name);

public sealed record StandInJob(
    string Uid, string? Title, string? Department, string? Position, string? Province, string? Town);

public sealed record StandInCandidate(
    string Uid,
    string? Username,
    string? Name,
    string? Surname,
    string? Email,
    string? Phone,
    string? PhotoUrl,
    string? Province,
    string? Town);

public sealed record StandInApplication(
    string Uid, string JobUid, string CandidateUid, int? ResumeId, string? Status, DateTimeOffset? AppliedAt);

public sealed record StandInSeed(
    StandInCompany Company,
    IReadOnlyList<StandInJob> Jobs,
    IReadOnlyList<StandInCandidate> Candidates,
    IReadOnlyList<StandInApplication> Applications);

public sealed record StandInSeedResult(int Jobs, int Candidates, int Applications, string SignedInAs);

public sealed class SeedStandInsHandler(
    RecruitingDbContext db,
    CacheInvalidator cache,
    IOptions<AuthOptions> auth,
    IHostEnvironment environment)
{
    public async Task<IResult> HandleAsync(StandInSeed seed, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return ApiResults.Forbidden("Bu uç nokta yalnızca geliştirme ortamında kullanılabilir.");
        }

        if (!await IsStandInDatabaseAsync(cancellationToken))
        {
            return ApiResults.Forbidden(
                "Bu veritabanı stand-in işaretini taşımıyor; yalnızca deploy/local ile kurulan geliştirme veritabanına yazılabilir.");
        }

        if (string.IsNullOrWhiteSpace(seed.Company.Uid))
        {
            return ApiResults.Validation("company.uid", "Şirket uid'i zorunludur.");
        }

        // With the dev identity on, the service signs every request in as the company whose
        // external_id matches it. Handing the synced company that id is what makes the ATS screens
        // answer for the real postings without a config edit and a restart; the previous holder
        // gives it up so the lookup stays unambiguous.
        string externalId = auth.Value.DevIdentity.Enabled
            ? auth.Value.DevIdentity.ExternalId
            : (seed.Company.ExternalId ?? seed.Company.Uid);

        if (auth.Value.DevIdentity.Enabled)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 UPDATE public.company SET external_id = NULL
                 WHERE external_id = {externalId} AND uid <> {seed.Company.Uid};
                 """,
                cancellationToken);
        }

        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO public.company (uid, external_id, company_name, status, is_account_completed)
             VALUES ({seed.Company.Uid}, {externalId}, {seed.Company.Name}, 'approved', true)
             ON CONFLICT (uid) DO UPDATE
               SET external_id = EXCLUDED.external_id,
                   company_name = EXCLUDED.company_name,
                   status = 'approved',
                   is_account_completed = true;
             """,
            cancellationToken);

        foreach (StandInJob job in seed.Jobs)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO public.company_job (uid, company_uid, title, department, position, province, town)
                 VALUES ({job.Uid}, {seed.Company.Uid}, {job.Title ?? ""}, {job.Department ?? ""},
                         {job.Position ?? ""}, {job.Province ?? ""}, {job.Town ?? ""})
                 ON CONFLICT (uid) DO UPDATE
                   SET company_uid = EXCLUDED.company_uid,
                       title = EXCLUDED.title,
                       department = EXCLUDED.department,
                       position = EXCLUDED.position,
                       province = EXCLUDED.province,
                       town = EXCLUDED.town;
                 """,
                cancellationToken);
        }

        foreach (StandInCandidate candidate in seed.Candidates)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO public.employee (uid, username, name, surname, email, phone, photo_url, province, town)
                 VALUES ({candidate.Uid}, {candidate.Username}, {candidate.Name}, {candidate.Surname},
                         {candidate.Email}, {candidate.Phone}, {candidate.PhotoUrl},
                         {candidate.Province}, {candidate.Town})
                 ON CONFLICT (uid) DO UPDATE
                   SET username = EXCLUDED.username,
                       name = EXCLUDED.name,
                       surname = EXCLUDED.surname,
                       email = EXCLUDED.email,
                       phone = EXCLUDED.phone,
                       photo_url = EXCLUDED.photo_url,
                       province = EXCLUDED.province,
                       town = EXCLUDED.town;
                 """,
                cancellationToken);
        }

        foreach (StandInApplication application in seed.Applications)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO public.job_application (uid, job_uid, applicant_uid, resume_id, application_status, applied_at)
                 VALUES ({application.Uid}, {application.JobUid}, {application.CandidateUid}, {application.ResumeId},
                         {application.Status ?? "pending"}, {application.AppliedAt ?? DateTimeOffset.UtcNow})
                 ON CONFLICT (uid) DO UPDATE
                   SET job_uid = EXCLUDED.job_uid,
                       applicant_uid = EXCLUDED.applicant_uid,
                       resume_id = EXCLUDED.resume_id,
                       application_status = EXCLUDED.application_status,
                       applied_at = EXCLUDED.applied_at;
                 """,
                cancellationToken);
        }

        // The directory and the list caches are keyed by company and job; seeded rows change both.
        await cache.InvalidateCompanyDirectoryAsync(seed.Company.Uid, cancellationToken);

        foreach (StandInJob job in seed.Jobs)
        {
            await cache.InvalidateJobAsync(job.Uid, cancellationToken);
        }

        return Results.Ok(
            new StandInSeedResult(seed.Jobs.Count, seed.Candidates.Count, seed.Applications.Count, externalId));
    }

    private async Task<bool> IsStandInDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();

        command.CommandText = "SELECT to_regclass('public.__standin_marker') IS NOT NULL";

        await db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            return await command.ExecuteScalarAsync(cancellationToken) is true;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
