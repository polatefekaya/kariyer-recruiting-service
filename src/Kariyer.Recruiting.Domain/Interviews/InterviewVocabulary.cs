namespace Kariyer.Recruiting.Domain.Interviews;

/// <summary>Where the interview happens (technical document §9).</summary>
public static class InterviewType
{
    public const string Video = "VIDEO";
    public const string Phone = "PHONE";
    public const string InPerson = "IN_PERSON";

    public static readonly IReadOnlyList<string> All = [Video, Phone, InPerson];

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [Video] = "Online",
        [Phone] = "Telefon",
        [InPerson] = "Ofiste",
    };

    public static bool IsValid(string? value) => value is not null && Labels.ContainsKey(value);

    public static string Label(string value) => Labels.TryGetValue(value, out string? l) ? l : value;
}

/// <summary>The operational state of the meeting itself.</summary>
public static class InterviewStatus
{
    public const string Scheduled = "SCHEDULED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    /// <summary>
    /// Not in the document, kept because "aday gelmedi" is a real outcome a recruiter records
    /// and it is not the same as CANCELLED — one is the candidate's no-show, the other is a
    /// meeting the company called off. Collapsing them would lose the only signal there is for
    /// no-show rates.
    /// </summary>
    public const string NoShow = "NO_SHOW";

    public static readonly IReadOnlyList<string> All = [Scheduled, Completed, Cancelled, NoShow];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Whether the candidate has answered the invitation.</summary>
public static class InterviewConfirmation
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
    public const string Expired = "EXPIRED";

    public static readonly IReadOnlyList<string> All = [Pending, Accepted, Declined, Expired];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>How the interview went, once it is completed.</summary>
public static class InterviewResult
{
    public const string Positive = "POSITIVE";
    public const string Negative = "NEGATIVE";
    public const string Undecided = "UNDECIDED";

    public static readonly IReadOnlyList<string> All = [Positive, Negative, Undecided];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Why a person is on the invitation.</summary>
public static class InterviewParticipantRole
{
    public const string Recruiter = "RECRUITER";
    public const string Interviewer = "INTERVIEWER";
    public const string Observer = "OBSERVER";

    public static readonly IReadOnlyList<string> All = [Recruiter, Interviewer, Observer];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
