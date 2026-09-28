using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Interviews;

public sealed record HiringUserResponse(string Uid, string Name, string? Position, string? PhotoUrl);

public sealed record InterviewDetailResponse(
    string Uid,
    string ApplicationUid,
    string JobUid,
    string CandidateUid,
    string Type,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string TimeZone,
    string? Location,
    string Status,
    string ConfirmationStatus,
    string? Result,
    string? Note,
    /// <summary>The recruiter's message to the candidate — what the invitation e-mail carries.</summary>
    string? CandidateMessage,
    HiringUserResponse Interviewer,
    HiringUserResponse InvitedBy,
    IReadOnlyList<InterviewParticipantResponse> Participants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InterviewParticipantResponse(string Email, string? Name, string Role);

public static class InterviewResponseMapping
{
    public static InterviewDetailResponse ToDetail(
        this Interview interview, IReadOnlyDictionary<string, CompanyMember> members) => new(
        interview.Uid,
        interview.ApplicationUid,
        interview.JobUid,
        interview.CandidateUid,
        interview.Type,
        interview.StartsAt,
        interview.DurationMinutes,
        interview.TimeZone,
        // The UI shows one "where" field whichever the type is.
        interview.Type == InterviewType.Video ? interview.VideoUrl : interview.Location,
        interview.Status,
        interview.ConfirmationStatus,
        interview.Result,
        interview.InternalNote,
        interview.CandidateMessage,
        Resolve(members, interview.InterviewerUid),
        Resolve(members, interview.CreatedBy),
        [.. interview.Participants.Select(p => new InterviewParticipantResponse(p.Email, p.Name, p.Role))],
        interview.CreatedAt,
        interview.UpdatedAt);

    private static HiringUserResponse Resolve(IReadOnlyDictionary<string, CompanyMember> members, string uid) =>
        members.TryGetValue(uid, out CompanyMember? member)
            ? new HiringUserResponse(member.Uid, member.Name, member.Position, member.PhotoUrl)
            : new HiringUserResponse(uid, uid, null, null);
}
