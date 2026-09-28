using Kariyer.Recruiting.Domain.Pipeline;

namespace Kariyer.Recruiting.Domain.Ports;

public sealed record ApplicationListQuery
{
    /// <summary>Null for the company-wide list the Adaylar screen uses.</summary>
    public string? JobUid { get; init; }

    public required string CompanyUid { get; init; }

    public string? Stage { get; init; }

    public string? Search { get; init; }

    public string? CandidateUid { get; init; }

    public DateTimeOffset? AppliedFrom { get; init; }

    public DateTimeOffset? AppliedTo { get; init; }

    public ApplicationSort Sort { get; init; } = ApplicationSort.AppliedAtDesc;

    public int Page { get; init; } = 1;

    public int Limit { get; init; } = 20;

    public int Offset => (Page - 1) * Limit;
}

public enum ApplicationSort
{
    AppliedAtDesc,
    AppliedAtAsc,
    ScoreDesc,
    StageAsc,
}

public sealed record ApplicationListRow
{
    public required string ApplicationUid { get; init; }

    public required string JobUid { get; init; }

    public required string JobTitle { get; init; }

    public required string CandidateUid { get; init; }

    public string? CandidateName { get; init; }

    public string? CandidateSurname { get; init; }

    public string? CandidateEmail { get; init; }

    public string? CandidatePhone { get; init; }

    public string? CandidatePhotoUrl { get; init; }

    public string? Location { get; init; }

    public required string Stage { get; init; }

    public int? Score { get; init; }

    public int? ResumeId { get; init; }

    public DateTimeOffset AppliedAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public bool HasNote { get; init; }

    public InterviewSummary? NextInterview { get; init; }
}

public sealed record InterviewSummary(
    string Uid,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string Type,
    string Status,
    string ConfirmationStatus,
    string? InterviewerName,
    string? InvitedByName);

public sealed record ApplicationPage(
    IReadOnlyList<ApplicationListRow> Items,
    int Total,
    IReadOnlyDictionary<string, int> Stats);

public sealed record ApplicationSummary
{
    public required string ApplicationUid { get; init; }

    public required string JobUid { get; init; }

    public required string JobTitle { get; init; }

    public required string CompanyUid { get; init; }

    public required string CandidateUid { get; init; }

    public string? CandidateName { get; init; }

    public string? CandidateSurname { get; init; }

    public string? CandidateEmail { get; init; }

    public string? CandidatePhone { get; init; }

    public string? CandidatePhotoUrl { get; init; }

    public int? ResumeId { get; init; }

    public string LegacyStatus { get; init; } = string.Empty;

    public DateTimeOffset AppliedAt { get; init; }

    public string Stage { get; init; } = ApplicationStage.New;
}

/// <summary>Reads that join this service's schema with the read-only projections.</summary>
public interface IApplicationReadStore
{
    Task<ApplicationPage> ListAsync(ApplicationListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> StatsAsync(
        string jobUid, string companyUid, CancellationToken cancellationToken);

    Task<ApplicationSummary?> FindAsync(
        string applicationUid, string companyUid, CancellationToken cancellationToken);

    Task<bool> JobBelongsToCompanyAsync(string jobUid, string companyUid, CancellationToken cancellationToken);
}
