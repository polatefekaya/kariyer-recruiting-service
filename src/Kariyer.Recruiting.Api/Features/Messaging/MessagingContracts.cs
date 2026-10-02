using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Messaging;

/// <summary>One applicant in the "Adaylarla iletişime geç" list.</summary>
public sealed record MessageRecipientResponse(
    string ApplicationUid,
    string CandidateUid,
    string FullName,
    string? Email,
    string? AvatarUrl,
    string Stage,
    string StageLabel,
    DateTimeOffset? LastMessagedAt,
    int MessageCount);

public static class MessagingMapping
{
    public static MessageRecipientResponse ToResponse(this MessageAudienceRow row) => new(
        row.ApplicationUid,
        row.CandidateUid,
        $"{row.CandidateName} {row.CandidateSurname}".Trim(),
        string.IsNullOrWhiteSpace(row.CandidateEmail) ? null : row.CandidateEmail,
        row.CandidatePhotoUrl,
        row.Stage,
        ApplicationStage.Label(row.Stage),
        row.LastMessagedAt,
        row.MessageCount);
}
