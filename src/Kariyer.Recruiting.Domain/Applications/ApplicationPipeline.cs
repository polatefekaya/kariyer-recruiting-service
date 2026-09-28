using Kariyer.Recruiting.Domain.Abstractions;
using Kariyer.Recruiting.Domain.Pipeline;

namespace Kariyer.Recruiting.Domain.Applications;

public sealed class ApplicationPipeline : AggregateRoot
{
    private ApplicationPipeline()
    {
    }

    public string ApplicationUid { get; private set; } = string.Empty;

    public string JobUid { get; private set; } = string.Empty;

    public string CompanyUid { get; private set; } = string.Empty;

    public string CandidateUid { get; private set; } = string.Empty;

    public string Stage { get; private set; } = ApplicationStage.New;

    public string? Reason { get; private set; }

    public string? ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public uint Version { get; private set; }

    public static ApplicationPipeline Start(
        string applicationUid,
        string jobUid,
        string companyUid,
        string candidateUid,
        string stage,
        DateTimeOffset now)
    {
        if (!ApplicationStage.IsValid(stage))
        {
            throw new ArgumentException($"Unknown stage: {stage}", nameof(stage));
        }

        return new ApplicationPipeline
        {
            ApplicationUid = applicationUid,
            JobUid = jobUid,
            CompanyUid = companyUid,
            CandidateUid = candidateUid,
            Stage = stage,
            ChangedAt = now,
            CreatedAt = now,
        };
    }

    public StageChanged MoveTo(string stage, string actorUid, string? reason, DateTimeOffset now)
    {
        StageTransitions.Assert(Stage, stage);

        string from = Stage;

        Stage = stage;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ChangedBy = actorUid;
        ChangedAt = now;

        StageChanged change = new(ApplicationUid, JobUid, CompanyUid, CandidateUid, from, stage, actorUid, Reason, now);

        Raise(change);

        return change;
    }
}

public sealed record StageChanged(
    string ApplicationUid,
    string JobUid,
    string CompanyUid,
    string CandidateUid,
    string FromStage,
    string ToStage,
    string ActorUid,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
