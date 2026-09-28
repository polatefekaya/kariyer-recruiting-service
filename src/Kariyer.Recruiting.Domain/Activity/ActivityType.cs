namespace Kariyer.Recruiting.Domain.Activity;

/// <summary>
/// What the audit trail records (technical document §13).
///
/// The document calls audit mandatory for status changes, notes, invitations and messages. The
/// set below adds the two CV events, because "hangi CV'yi kim görüntüledi" is the question KVKK
/// requests actually ask, and the company already spends a view right to open one.
/// </summary>
public static class ActivityType
{
    public const string ApplicationCreated = "APPLICATION_CREATED";
    public const string StageChanged = "STAGE_CHANGED";
    public const string NoteSaved = "NOTE_SAVED";
    public const string NoteCleared = "NOTE_CLEARED";
    public const string InterviewCreated = "INTERVIEW_CREATED";
    public const string InterviewUpdated = "INTERVIEW_UPDATED";
    public const string InterviewCancelled = "INTERVIEW_CANCELLED";
    public const string InterviewCompleted = "INTERVIEW_COMPLETED";
    public const string MessageSent = "MESSAGE_SENT";
    public const string CvViewed = "CV_VIEWED";
    public const string CvDownloaded = "CV_DOWNLOADED";
    public const string ExportCreated = "EXPORT_CREATED";

    public static readonly IReadOnlyList<string> All =
    [
        ApplicationCreated, StageChanged, NoteSaved, NoteCleared,
        InterviewCreated, InterviewUpdated, InterviewCancelled, InterviewCompleted,
        MessageSent, CvViewed, CvDownloaded, ExportCreated
    ];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
