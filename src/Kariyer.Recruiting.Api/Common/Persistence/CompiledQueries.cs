using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Common.Persistence;

/// <summary>
/// Hot-path queries compiled once. The aggregate lookups are TRACKED on purpose: the context
/// defaults to NoTracking for reads, and an untracked aggregate loaded for a write is mutated
/// and then silently dropped by SaveChanges.
/// </summary>
public static class CompiledQueries
{
    public static readonly Func<RecruitingDbContext, string, string, Task<bool>> JobBelongsToCompany =
        EF.CompileAsyncQuery((RecruitingDbContext db, string jobUid, string companyUid) =>
            db.CompanyJobs.AsNoTracking().Any(j => j.Uid == jobUid && j.CompanyUid == companyUid));

    public static readonly Func<RecruitingDbContext, string, Task<Domain.Applications.ApplicationPipeline?>> Pipeline =
        EF.CompileAsyncQuery((RecruitingDbContext db, string applicationUid) =>
            db.Pipelines.AsTracking().FirstOrDefault(p => p.ApplicationUid == applicationUid));

    public static readonly Func<RecruitingDbContext, string, Task<Domain.Notes.ApplicationNote?>> Note =
        EF.CompileAsyncQuery((RecruitingDbContext db, string applicationUid) =>
            db.Notes.AsTracking().FirstOrDefault(n => n.ApplicationUid == applicationUid));
}
