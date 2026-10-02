using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.SavedFilters;

namespace Kariyer.Recruiting.Domain.Ports;

public interface IApplicationPipelineRepository
{
    Task<ApplicationPipeline?> FindAsync(string applicationUid, CancellationToken cancellationToken);

    void Add(ApplicationPipeline pipeline);
}

public interface IInterviewRepository
{
    Task<Interview?> FindAsync(string uid, CancellationToken cancellationToken);

    Task<IReadOnlyList<Interview>> ListByJobAsync(string jobUid, CancellationToken cancellationToken);

    void Add(Interview interview);
}

public interface IApplicationNoteRepository
{
    Task<ApplicationNote?> FindAsync(string applicationUid, CancellationToken cancellationToken);

    void Add(ApplicationNote note);

    void Remove(ApplicationNote note);
}

public interface ICandidateMessageRepository
{
    void Add(Messaging.CandidateMessage message);
}

public interface IActivityWriter
{
    void Write(ActivityEntry entry);
}

public interface ISavedFilterRepository
{
    Task<IReadOnlyList<SavedFilter>> ListAsync(string companyUid, string userUid, CancellationToken cancellationToken);

    Task<SavedFilter?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(SavedFilter filter);

    void Remove(SavedFilter filter);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
