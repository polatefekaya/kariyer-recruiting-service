using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Domain.Interviews;

/// <summary>
/// What a valid interview invitation looks like (technical document §9, form alanları).
///
/// Validation lives in the domain rather than in the endpoint because the same rules are
/// applied twice — when the invitation is created and when it is rescheduled — and a rule that
/// exists in two endpoints is a rule that will eventually exist in two versions.
/// </summary>
public static class InterviewRules
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 180;
    public const int MaxCandidateMessageLength = 500;
    public const int MaxParticipants = 10;

    /// <summary>
    /// How far in the past a start time may land before it is rejected. Not zero: a recruiter
    /// filling the form at 14:59 for a 15:00 slot can easily submit at 15:00:02, and refusing
    /// that as "geçmiş tarih" would be nonsense. A meeting genuinely in the past is recorded by
    /// creating it and completing it, not by back-dating an invitation.
    /// </summary>
    public static readonly TimeSpan StartTimeGrace = TimeSpan.FromMinutes(5);

    public static ValidationResult Validate(InterviewDraft draft, DateTimeOffset now)
    {
        ValidationResult result = new();

        if (!InterviewType.IsValid(draft.Type))
        {
            result.Add("type", $"Görüşme türü şunlardan biri olmalı: {string.Join(", ", InterviewType.All)}.");
        }

        if (draft.DurationMinutes < MinDurationMinutes || draft.DurationMinutes > MaxDurationMinutes)
        {
            result.Add("durationMinutes", $"Süre {MinDurationMinutes}-{MaxDurationMinutes} dakika arasında olmalı.");
        }

        if (draft.StartsAt < now - StartTimeGrace)
        {
            result.Add("startsAt", "Görüşme tarihi geçmiş bir zamana ayarlanamaz.");
        }

        if (string.IsNullOrWhiteSpace(draft.TimeZone))
        {
            result.Add("timezone", "Saat dilimi zorunludur.");
        }
        else if (!IsKnownTimeZone(draft.TimeZone))
        {
            // Rejected rather than silently defaulted: an invitation stored with a time zone the
            // server could not resolve is an invitation whose displayed time depends on who is
            // reading it, and the candidate and the interviewer would see different clocks.
            result.Add("timezone", $"Bilinmeyen saat dilimi: {draft.TimeZone}.");
        }

        switch (draft.Type)
        {
            case InterviewType.Video when string.IsNullOrWhiteSpace(draft.VideoUrl):
                result.Add("videoUrl", "Online görüşme için toplantı bağlantısı zorunludur.");
                break;

            case InterviewType.Video when !IsHttpUrl(draft.VideoUrl!):
                result.Add("videoUrl", "Toplantı bağlantısı geçerli bir http(s) adresi olmalı.");
                break;

            case InterviewType.InPerson when string.IsNullOrWhiteSpace(draft.Location):
                result.Add("location", "Yüz yüze görüşme için adres zorunludur.");
                break;
        }

        if (draft.CandidateMessage is { Length: > MaxCandidateMessageLength })
        {
            result.Add("candidateMessage", $"Aday mesajı en fazla {MaxCandidateMessageLength} karakter olabilir.");
        }

        if (string.IsNullOrWhiteSpace(draft.InterviewerUid))
        {
            result.Add("interviewerUid", "Görüşmeyi yapacak kişiyi seçin.");
        }

        if (draft.Participants.Count > MaxParticipants)
        {
            result.Add("participants", $"En fazla {MaxParticipants} katılımcı eklenebilir.");
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (InterviewParticipantDraft participant in draft.Participants)
        {
            if (!IsEmail(participant.Email))
            {
                result.Add("participants", $"Geçersiz e-posta: {participant.Email}");
            }
            else if (!seen.Add(participant.Email.Trim()))
            {
                // Duplicates are rejected, not de-duplicated: the caller asked for something
                // incoherent, and silently dropping half of it makes the "3 kişi davet ettim"
                // in the recruiter's head disagree with the two invitations that went out.
                result.Add("participants", $"Aynı e-posta birden fazla kez eklenmiş: {participant.Email}");
            }

            if (!InterviewParticipantRole.IsValid(participant.Role))
            {
                result.Add("participants", $"Geçersiz katılımcı rolü: {participant.Role}");
            }
        }

        return result;
    }

    private static bool IsKnownTimeZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Deliberately permissive. The only thing this can usefully rule out is a value that could
    /// never be delivered; anything stricter starts rejecting addresses that work (plus tags,
    /// long TLDs, IDN domains), and the real verification is the e-mail either arriving or not.
    /// </summary>
    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int at = value.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at < value.Length - 1
            && value.IndexOf('@', at + 1) < 0
            && !value.Contains(' ', StringComparison.Ordinal)
            && value.LastIndexOf('.') > at + 1;
    }
}

/// <summary>The fields an invitation is created or rescheduled with.</summary>
public sealed record InterviewDraft
{
    public required string Type { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required int DurationMinutes { get; init; }

    public required string TimeZone { get; init; }

    public string? VideoUrl { get; init; }

    public string? Location { get; init; }

    /// <summary>Sent to the candidate with the invitation; not an internal note.</summary>
    public string? CandidateMessage { get; init; }

    /// <summary>The company user running the interview; shown on the board and the applicant row.</summary>
    public required string InterviewerUid { get; init; }

    public IReadOnlyList<InterviewParticipantDraft> Participants { get; init; } = [];
}

public sealed record InterviewParticipantDraft(string Email, string Role, string? Name = null);
