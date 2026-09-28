using Kariyer.Recruiting.Domain.Abstractions;

namespace Kariyer.Recruiting.Domain.Interviews;

public sealed class Interview : AggregateRoot
{
    private readonly List<InterviewParticipant> _participants = [];

    private Interview()
    {
    }

    public string Uid { get; private set; } = string.Empty;

    public string ApplicationUid { get; private set; } = string.Empty;

    public string JobUid { get; private set; } = string.Empty;

    public string CompanyUid { get; private set; } = string.Empty;

    public string CandidateUid { get; private set; } = string.Empty;

    public string Type { get; private set; } = InterviewType.Video;

    public DateTimeOffset StartsAt { get; private set; }

    public int DurationMinutes { get; private set; }

    public string TimeZone { get; private set; } = "Europe/Istanbul";

    public string? VideoUrl { get; private set; }

    public string? Location { get; private set; }

    public string? CandidateMessage { get; private set; }

    public string? InternalNote { get; private set; }

    public string Status { get; private set; } = InterviewStatus.Scheduled;

    public string ConfirmationStatus { get; private set; } = InterviewConfirmation.Pending;

    public string? Result { get; private set; }

    public string InterviewerUid { get; private set; } = string.Empty;

    /// <summary>Who sent the invitation ("davet eden"); not always the interviewer.</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<InterviewParticipant> Participants => _participants;

    public DateTimeOffset EndsAt => StartsAt.AddMinutes(DurationMinutes);

    public static Interview Schedule(
        string uid,
        string applicationUid,
        string jobUid,
        string companyUid,
        string candidateUid,
        InterviewDraft draft,
        string createdBy,
        string? internalNote,
        DateTimeOffset now)
    {
        Interview interview = new()
        {
            Uid = uid,
            ApplicationUid = applicationUid,
            JobUid = jobUid,
            CompanyUid = companyUid,
            CandidateUid = candidateUid,
            CreatedBy = createdBy,
            InternalNote = Trim(internalNote),
            CreatedAt = now,
            UpdatedAt = now,
        };

        interview.Apply(draft);
        interview.Raise(new InterviewScheduled(interview, now));

        return interview;
    }

    public void Reschedule(InterviewDraft draft, string actorUid, DateTimeOffset now)
    {
        EnsureActive();

        DateTimeOffset previousStart = StartsAt;
        bool candidateVisibleChange =
            draft.StartsAt != StartsAt
            || draft.DurationMinutes != DurationMinutes
            || !string.Equals(draft.Type, Type, StringComparison.Ordinal)
            || !string.Equals(draft.VideoUrl, VideoUrl, StringComparison.Ordinal)
            || !string.Equals(draft.Location, Location, StringComparison.Ordinal);

        Apply(draft);
        UpdatedAt = now;

        if (!candidateVisibleChange)
        {
            return;
        }

        // A time the candidate already agreed to has moved, so the answer is no longer theirs.
        ConfirmationStatus = InterviewConfirmation.Pending;

        Raise(new InterviewRescheduled(this, previousStart, actorUid, now));
    }

    public void Cancel(string actorUid, string? candidateMessage, DateTimeOffset now)
    {
        EnsureActive();

        Status = InterviewStatus.Cancelled;
        CandidateMessage = Trim(candidateMessage) ?? CandidateMessage;
        UpdatedAt = now;

        Raise(new InterviewCancelled(this, actorUid, now));
    }

    public void Complete(string result, string? internalNote, DateTimeOffset now)
    {
        if (!InterviewResult.IsValid(result))
        {
            throw new ArgumentException($"Unknown result: {result}", nameof(result));
        }

        Status = InterviewStatus.Completed;
        Result = result;
        InternalNote = Trim(internalNote) ?? InternalNote;
        UpdatedAt = now;
    }

    public void MarkNoShow(DateTimeOffset now)
    {
        EnsureActive();

        Status = InterviewStatus.NoShow;
        UpdatedAt = now;
    }

    public void Confirm(string confirmation, DateTimeOffset now)
    {
        if (!InterviewConfirmation.IsValid(confirmation))
        {
            throw new ArgumentException($"Unknown confirmation: {confirmation}", nameof(confirmation));
        }

        ConfirmationStatus = confirmation;
        UpdatedAt = now;
    }

    public bool IsOngoing(DateTimeOffset now) =>
        Status == InterviewStatus.Scheduled && StartsAt <= now && now < EndsAt;

    public bool IsUpcoming(DateTimeOffset now) =>
        Status == InterviewStatus.Scheduled && StartsAt > now;

    private void Apply(InterviewDraft draft)
    {
        Type = draft.Type;
        InterviewerUid = draft.InterviewerUid;
        StartsAt = draft.StartsAt;
        DurationMinutes = draft.DurationMinutes;
        TimeZone = draft.TimeZone;
        VideoUrl = draft.Type == InterviewType.Video ? Trim(draft.VideoUrl) : null;
        Location = draft.Type == InterviewType.Video ? null : Trim(draft.Location);
        CandidateMessage = Trim(draft.CandidateMessage);

        _participants.Clear();
        _participants.AddRange(draft.Participants.Select(p =>
            new InterviewParticipant(p.Email.Trim(), p.Role, Trim(p.Name))));
    }

    private void EnsureActive()
    {
        if (Status is InterviewStatus.Cancelled or InterviewStatus.Completed)
        {
            throw new InterviewNotActiveException(Uid, Status);
        }
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record InterviewParticipant(string Email, string Role, string? Name);

public sealed class InterviewNotActiveException(string uid, string status)
    : InvalidOperationException($"Interview {uid} is {status} and cannot be changed.")
{
    public string Status { get; } = status;
}

public sealed record InterviewScheduled(Interview Interview, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record InterviewRescheduled(
    Interview Interview,
    DateTimeOffset PreviousStartsAt,
    string ActorUid,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record InterviewCancelled(
    Interview Interview,
    string ActorUid,
    DateTimeOffset OccurredAt) : IDomainEvent;
