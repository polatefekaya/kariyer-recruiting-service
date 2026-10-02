using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Domain.Messaging;

/// <summary>What a message to applicants may look like, and to how many of them it may go at once.</summary>
public static class MessageRules
{
    public const int MaxSubjectLength = 150;

    public const int MaxBodyLength = 2000;

    /// <summary>
    /// One send's ceiling. Large enough for any posting's whole pipeline stage, small enough that
    /// a mistaken send is a contained one and the event stays a reasonable size on the bus.
    /// </summary>
    public const int MaxRecipients = 500;

    public static ValidationResult Validate(string? subject, string? body, int recipientCount)
    {
        ValidationResult result = new();

        if (string.IsNullOrWhiteSpace(body))
        {
            result.Add("body", "Mesaj boş olamaz.");
        }
        else if (body.Trim().Length > MaxBodyLength)
        {
            result.Add("body", $"Mesaj en fazla {MaxBodyLength} karakter olabilir.");
        }

        if (subject is { Length: > MaxSubjectLength })
        {
            result.Add("subject", $"Konu en fazla {MaxSubjectLength} karakter olabilir.");
        }

        if (recipientCount == 0)
        {
            result.Add("applicationUids", "En az bir aday seçin.");
        }
        else if (recipientCount > MaxRecipients)
        {
            result.Add("applicationUids", $"Tek seferde en fazla {MaxRecipients} adaya mesaj gönderilebilir.");
        }

        return result;
    }

    public static string? NormalizeSubject(string? subject) =>
        string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
}
