using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.SavedFilters;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Common.Persistence;

public sealed class ApplicationPipelineRepository(RecruitingDbContext db) : IApplicationPipelineRepository
{
    public Task<ApplicationPipeline?> FindAsync(string applicationUid, CancellationToken cancellationToken) =>
        CompiledQueries.Pipeline(db, applicationUid);

    public void Add(ApplicationPipeline pipeline) => db.Pipelines.Add(pipeline);
}

public sealed class InterviewRepository(RecruitingDbContext db) : IInterviewRepository
{
    public Task<Interview?> FindAsync(string uid, CancellationToken cancellationToken) =>
        db.Interviews.AsTracking().Include(i => i.Participants)
            .FirstOrDefaultAsync(i => i.Uid == uid, cancellationToken);

    public async Task<IReadOnlyList<Interview>> ListByJobAsync(string jobUid, CancellationToken cancellationToken) =>
        await db.Interviews
            .AsNoTracking()
            .Where(i => i.JobUid == jobUid)
            .OrderBy(i => i.StartsAt)
            .ToListAsync(cancellationToken);

    public void Add(Interview interview) => db.Interviews.Add(interview);
}

public sealed class ApplicationNoteRepository(RecruitingDbContext db) : IApplicationNoteRepository
{
    public Task<ApplicationNote?> FindAsync(string applicationUid, CancellationToken cancellationToken) =>
        CompiledQueries.Note(db, applicationUid);

    public void Add(ApplicationNote note) => db.Notes.Add(note);

    public void Remove(ApplicationNote note) => db.Notes.Remove(note);
}

public sealed class CandidateMessageRepository(RecruitingDbContext db) : ICandidateMessageRepository
{
    public void Add(Domain.Messaging.CandidateMessage message) => db.Messages.Add(message);
}

public sealed class ActivityWriter(RecruitingDbContext db) : IActivityWriter
{
    public void Write(ActivityEntry entry) => db.Activity.Add(entry);
}

public sealed class SavedFilterRepository(RecruitingDbContext db) : ISavedFilterRepository
{
    public async Task<IReadOnlyList<SavedFilter>> ListAsync(
        string companyUid, string userUid, CancellationToken cancellationToken) =>
        await db.SavedFilters
            .AsNoTracking()
            .Where(f => f.CompanyUid == companyUid && f.UserUid == userUid)
            .OrderByDescending(f => f.IsDefault)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

    public Task<SavedFilter?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.SavedFilters.AsTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public void Add(SavedFilter filter) => db.SavedFilters.Add(filter);

    public void Remove(SavedFilter filter) => db.SavedFilters.Remove(filter);
}

public sealed class UnitOfWork(RecruitingDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
