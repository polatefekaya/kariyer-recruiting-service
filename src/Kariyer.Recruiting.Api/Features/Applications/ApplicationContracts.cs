using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Applications;

public sealed record ApplicationListItemResponse(
    string Id,
    JobRefResponse Job,
    CandidateResponse Candidate,
    string Stage,
    string StageLabel,
    int? Score,
    int? ResumeId,
    DateTimeOffset AppliedAt,
    DateTimeOffset? LastActivityAt,
    bool HasNote,
    InterviewSummaryResponse? NextInterview,
    IReadOnlyList<string> AllowedActions);

public sealed record JobRefResponse(string Uid, string Title);

public sealed record CandidateResponse(
    string Id,
    string FullName,
    string? Email,
    string? Phone,
    string? Location,
    string? AvatarUrl);

public sealed record InterviewSummaryResponse(
    string Id,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string Type,
    string Status,
    string ConfirmationStatus);

public sealed record ApplicationListResponse(
    IReadOnlyList<ApplicationListItemResponse> Items,
    PaginationResponse Pagination,
    IReadOnlyDictionary<string, int> Stats);

public sealed record PaginationResponse(int Page, int Limit, int Total, int TotalPages);

public static class ApplicationMapping
{
    public static ApplicationListItemResponse ToResponse(this ApplicationListRow row) => new(
        row.ApplicationUid,
        new JobRefResponse(row.JobUid, row.JobTitle),
        new CandidateResponse(
            row.CandidateUid,
            $"{row.CandidateName} {row.CandidateSurname}".Trim(),
            row.CandidateEmail,
            row.CandidatePhone,
            row.Location,
            row.CandidatePhotoUrl),
        row.Stage,
        Domain.Pipeline.ApplicationStage.Label(row.Stage),
        row.Score,
        row.ResumeId,
        row.AppliedAt,
        row.LastActivityAt,
        row.HasNote,
        row.NextInterview is null
            ? null
            : new InterviewSummaryResponse(
                row.NextInterview.Uid,
                row.NextInterview.StartsAt,
                row.NextInterview.DurationMinutes,
                row.NextInterview.Type,
                row.NextInterview.Status,
                row.NextInterview.ConfirmationStatus),
        Domain.Pipeline.StageTransitions.From(row.Stage));
}
