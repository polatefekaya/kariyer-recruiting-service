using Kariyer.Recruiting.Api.Common.Persistence.Projections;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.Search;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Common.Persistence;

public sealed class ApplicationReadStore(RecruitingDbContext db) : IApplicationReadStore
{
    public async Task<bool> JobBelongsToCompanyAsync(
        string jobUid, string companyUid, CancellationToken cancellationToken) =>
        await CompiledQueries.JobBelongsToCompany(db, jobUid, companyUid);

    public async Task<ApplicationPage> ListAsync(ApplicationListQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Source> source = Filter(query);

        int total = await source.CountAsync(cancellationToken);

        List<Row> page = await Paginate(source, query)
            .Select(x => new Row(
                x.Application.Uid,
                x.Application.JobUid,
                x.Job.Title,
                x.Application.ApplicantUid,
                x.Candidate.Name,
                x.Candidate.Surname,
                x.Candidate.Email,
                x.Candidate.Phone,
                x.Candidate.PhotoUrl,
                x.Candidate.Province,
                x.Candidate.Town,
                x.Application.ResumeId,
                x.Application.AppliedAt,
                x.Stage))
            .ToListAsync(cancellationToken);

        string[] uids = [.. page.Select(r => r.ApplicationUid)];

        Dictionary<string, InterviewSummary> interviews = await NextInterviewsAsync(uids, cancellationToken);
        HashSet<string> noted = await NotedApplicationsAsync(uids, cancellationToken);
        Dictionary<string, DateTimeOffset> lastActivity = await LastActivityAsync(uids, cancellationToken);

        List<ApplicationListRow> items =
        [
            .. page.Select(r => new ApplicationListRow
            {
                ApplicationUid = r.ApplicationUid,
                JobUid = r.JobUid,
                JobTitle = r.JobTitle,
                CandidateUid = r.CandidateUid,
                CandidateName = r.Name,
                CandidateSurname = r.Surname,
                CandidateEmail = r.Email,
                CandidatePhone = r.Phone,
                CandidatePhotoUrl = r.PhotoUrl,
                Location = Join(r.Town, r.Province),
                Stage = r.Stage,
                ResumeId = r.ResumeId,
                AppliedAt = r.AppliedAt,
                HasNote = noted.Contains(r.ApplicationUid),
                LastActivityAt = lastActivity.TryGetValue(r.ApplicationUid, out DateTimeOffset at) ? at : null,
                NextInterview = interviews.GetValueOrDefault(r.ApplicationUid),
            })
        ];

        IReadOnlyDictionary<string, int> stats = query.JobUid is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await StatsAsync(query.JobUid, query.CompanyUid, cancellationToken);

        return new ApplicationPage(items, total, stats);
    }

    public async Task<IReadOnlyDictionary<string, int>> StatsAsync(
        string jobUid, string companyUid, CancellationToken cancellationToken)
    {
        List<StageCount> counts = await db.JobApplications
            .AsNoTracking()
            .Where(a => a.JobUid == jobUid)
            .GroupJoin(
                db.Pipelines.Where(p => p.CompanyUid == companyUid),
                a => a.Uid,
                p => p.ApplicationUid,
                (a, p) => new { a.ApplicationStatus, Pipeline = p.FirstOrDefault() })
            .GroupBy(x => x.Pipeline != null ? x.Pipeline.Stage : RecruitingFunctions.StageFromLegacy(x.ApplicationStatus))
            .Select(g => new StageCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        Dictionary<string, int> stats = new(StringComparer.Ordinal);
        int all = 0;

        foreach (StageCount count in counts)
        {
            string stage = Domain.Pipeline.ApplicationStage.IsValid(count.Stage)
                ? count.Stage
                : LegacyStageMapping.ToStage(count.Stage);

            stats[stage] = stats.GetValueOrDefault(stage) + count.Count;
            all += count.Count;
        }

        stats["ALL"] = all;

        return stats;
    }

    public async Task<ApplicationSummary?> FindAsync(
        string applicationUid, string companyUid, CancellationToken cancellationToken)
    {
        var row = await (
            from application in db.JobApplications.AsNoTracking()
            join job in db.CompanyJobs.AsNoTracking() on application.JobUid equals job.Uid
            join candidate in db.Employees.AsNoTracking() on application.ApplicantUid equals candidate.Uid
            where application.Uid == applicationUid && job.CompanyUid == companyUid
            select new
            {
                application.Uid,
                application.JobUid,
                job.Title,
                job.CompanyUid,
                application.ApplicantUid,
                candidate.Name,
                candidate.Surname,
                candidate.Email,
                candidate.Phone,
                candidate.PhotoUrl,
                application.ResumeId,
                application.ApplicationStatus,
                application.AppliedAt,
                Stage = db.Pipelines
                    .Where(p => p.ApplicationUid == application.Uid)
                    .Select(p => p.Stage)
                    .FirstOrDefault(),
            }).FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new ApplicationSummary
        {
            ApplicationUid = row.Uid,
            JobUid = row.JobUid,
            JobTitle = row.Title,
            CompanyUid = row.CompanyUid ?? companyUid,
            CandidateUid = row.ApplicantUid,
            CandidateName = row.Name,
            CandidateSurname = row.Surname,
            CandidateEmail = row.Email,
            CandidatePhone = row.Phone,
            CandidatePhotoUrl = row.PhotoUrl,
            ResumeId = row.ResumeId,
            LegacyStatus = row.ApplicationStatus,
            AppliedAt = row.AppliedAt,
            Stage = row.Stage ?? LegacyStageMapping.ToStage(row.ApplicationStatus),
        };
    }

    /// <summary>
    /// Filters run against the joined entities and the stage is a correlated subquery, so
    /// ordering and paging happen before the projection — a projected stage cannot be ordered by
    /// or folded in SQL.
    /// </summary>
    private IQueryable<Source> Filter(ApplicationListQuery query)
    {
        IQueryable<Source> source =
            from application in db.JobApplications.AsNoTracking()
            join job in db.CompanyJobs.AsNoTracking() on application.JobUid equals job.Uid
            join candidate in db.Employees.AsNoTracking() on application.ApplicantUid equals candidate.Uid
            where job.CompanyUid == query.CompanyUid
            select new Source
            {
                Application = application,
                Job = job,
                Candidate = candidate,
                Stage = db.Pipelines
                            .Where(p => p.ApplicationUid == application.Uid)
                            .Select(p => p.Stage)
                            .FirstOrDefault()
                        ?? RecruitingFunctions.StageFromLegacy(application.ApplicationStatus),
            };

        if (!string.IsNullOrWhiteSpace(query.JobUid))
        {
            source = source.Where(x => x.Application.JobUid == query.JobUid);
        }

        if (!string.IsNullOrWhiteSpace(query.CandidateUid))
        {
            source = source.Where(x => x.Application.ApplicantUid == query.CandidateUid);
        }

        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            source = source.Where(x => x.Stage == query.Stage);
        }

        if (query.AppliedFrom is { } from)
        {
            source = source.Where(x => x.Application.AppliedAt >= from);
        }

        if (query.AppliedTo is { } to)
        {
            source = source.Where(x => x.Application.AppliedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = $"%{TurkishCasing.Normalize(query.Search)}%";

            source = source.Where(x =>
                EF.Functions.Like(RecruitingFunctions.Fold(x.Candidate.Name ?? ""), term)
                || EF.Functions.Like(RecruitingFunctions.Fold(x.Candidate.Surname ?? ""), term)
                || EF.Functions.Like(RecruitingFunctions.Fold(x.Candidate.Email ?? ""), term));
        }

        return source;
    }

    private static IQueryable<Source> Paginate(IQueryable<Source> source, ApplicationListQuery query) =>
        Sort(source, query.Sort).Skip(query.Offset).Take(query.Limit);

    private static IQueryable<Source> Sort(IQueryable<Source> source, ApplicationSort sort) => sort switch
    {
        ApplicationSort.AppliedAtAsc => source
            .OrderBy(x => x.Application.AppliedAt)
            .ThenBy(x => x.Application.Uid),
        ApplicationSort.StageAsc => source
            .OrderBy(x => x.Stage)
            .ThenByDescending(x => x.Application.AppliedAt),
        _ => source
            .OrderByDescending(x => x.Application.AppliedAt)
            .ThenBy(x => x.Application.Uid),
    };

    public async Task<IReadOnlyList<MessageAudienceRow>> AudienceAsync(
        string jobUid,
        string companyUid,
        IReadOnlyCollection<string>? stages,
        IReadOnlyCollection<string>? applicationUids,
        CancellationToken cancellationToken)
    {
        IQueryable<Source> source = Filter(new ApplicationListQuery { JobUid = jobUid, CompanyUid = companyUid });

        if (stages is { Count: > 0 })
        {
            source = source.Where(x => stages.Contains(x.Stage));
        }

        if (applicationUids is not null)
        {
            source = source.Where(x => applicationUids.Contains(x.Application.Uid));
        }

        var rows = await Sort(source, ApplicationSort.AppliedAtDesc)
            .Select(x => new
            {
                x.Application.Uid,
                x.Application.ApplicantUid,
                x.Candidate.Name,
                x.Candidate.Surname,
                x.Candidate.Email,
                x.Candidate.PhotoUrl,
                x.Stage,
            })
            .ToListAsync(cancellationToken);

        string[] uids = [.. rows.Select(r => r.Uid)];

        var messaged = uids.Length == 0
            ? []
            : await db.Activity
                .AsNoTracking()
                .Where(a => uids.Contains(a.ApplicationUid) && a.Type == Domain.Activity.ActivityType.MessageSent)
                .GroupBy(a => a.ApplicationUid)
                .Select(g => new { ApplicationUid = g.Key, LastAt = g.Max(a => a.CreatedAt), Count = g.Count() })
                .ToListAsync(cancellationToken);

        var byApplication = messaged.ToDictionary(m => m.ApplicationUid, StringComparer.Ordinal);

        return
        [
            .. rows.Select(r => new MessageAudienceRow
            {
                ApplicationUid = r.Uid,
                CandidateUid = r.ApplicantUid,
                CandidateName = r.Name,
                CandidateSurname = r.Surname,
                CandidateEmail = r.Email,
                CandidatePhotoUrl = r.PhotoUrl,
                Stage = r.Stage,
                LastMessagedAt = byApplication.TryGetValue(r.Uid, out var m) ? m.LastAt : null,
                MessageCount = byApplication.TryGetValue(r.Uid, out var n) ? n.Count : 0,
            }),
        ];
    }

    private async Task<Dictionary<string, InterviewSummary>> NextInterviewsAsync(
        string[] applicationUids, CancellationToken cancellationToken)
    {
        if (applicationUids.Length == 0)
        {
            return [];
        }

        List<Interview> interviews = await db.Interviews
            .AsNoTracking()
            .Where(i => applicationUids.Contains(i.ApplicationUid) && i.Status == InterviewStatus.Scheduled)
            .OrderBy(i => i.StartsAt)
            .ToListAsync(cancellationToken);

        return interviews
            .GroupBy(i => i.ApplicationUid, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new InterviewSummary(
                    g.First().Uid,
                    g.First().StartsAt,
                    g.First().DurationMinutes,
                    g.First().Type,
                    g.First().Status,
                    g.First().ConfirmationStatus,
                    null,
                    null),
                StringComparer.Ordinal);
    }

    private async Task<HashSet<string>> NotedApplicationsAsync(
        string[] applicationUids, CancellationToken cancellationToken)
    {
        if (applicationUids.Length == 0)
        {
            return [];
        }

        List<string> noted = await db.Notes
            .AsNoTracking()
            .Where(n => applicationUids.Contains(n.ApplicationUid))
            .Select(n => n.ApplicationUid)
            .ToListAsync(cancellationToken);

        return [.. noted];
    }

    private async Task<Dictionary<string, DateTimeOffset>> LastActivityAsync(
        string[] applicationUids, CancellationToken cancellationToken)
    {
        if (applicationUids.Length == 0)
        {
            return [];
        }

        var rows = await db.Activity
            .AsNoTracking()
            .Where(a => applicationUids.Contains(a.ApplicationUid))
            .GroupBy(a => a.ApplicationUid)
            .Select(g => new { ApplicationUid = g.Key, LastAt = g.Max(a => a.CreatedAt) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ApplicationUid, r => r.LastAt, StringComparer.Ordinal);
    }

    private static string? Join(string? town, string? province) =>
        string.Join(", ", new[] { town, province }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } s
            ? s
            : null;

    private sealed class Source
    {
        public required JobApplicationProjection Application { get; init; }

        public required CompanyJobProjection Job { get; init; }

        public required EmployeeProjection Candidate { get; init; }

        public required string Stage { get; init; }
    }

    private sealed record Row(
        string ApplicationUid,
        string JobUid,
        string JobTitle,
        string CandidateUid,
        string? Name,
        string? Surname,
        string? Email,
        string? Phone,
        string? PhotoUrl,
        string? Province,
        string? Town,
        int? ResumeId,
        DateTimeOffset AppliedAt,
        string Stage);

    private sealed record StageCount(string Stage, int Count);
}
