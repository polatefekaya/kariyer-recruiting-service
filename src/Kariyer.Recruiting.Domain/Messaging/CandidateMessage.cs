namespace Kariyer.Recruiting.Domain.Messaging;

/// <summary>
/// One message a company sent to a group of its applicants from the employer portal. Kept once
/// per send — the recipients are recorded as MESSAGE_SENT entries in the activity log, each
/// pointing back here — so "what did we tell them" survives without copying the body per person.
/// </summary>
public sealed class CandidateMessage
{
    private CandidateMessage()
    {
    }

    public string Uid { get; private set; } = string.Empty;

    public string JobUid { get; private set; } = string.Empty;

    public string CompanyUid { get; private set; } = string.Empty;

    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public string SentByUid { get; private set; } = string.Empty;

    public string? SentByName { get; private set; }

    public int RecipientCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static CandidateMessage Create(
        string uid,
        string jobUid,
        string companyUid,
        string? subject,
        string body,
        string sentByUid,
        string? sentByName,
        int recipientCount,
        DateTimeOffset now) => new()
        {
            Uid = uid,
            JobUid = jobUid,
            CompanyUid = companyUid,
            Subject = MessageRules.NormalizeSubject(subject),
            Body = body.Trim(),
            SentByUid = sentByUid,
            SentByName = sentByName,
            RecipientCount = recipientCount,
            CreatedAt = now,
        };
}
