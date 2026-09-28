using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Domain.Notes;

/// <summary>
/// The team's note about one applicant (technical document §8, not ekleme kuralları).
///
/// ONE note per application, edited in place, rather than the document's append-only list: the
/// product decision is that this is a notepad, not a comment thread. The audit the document
/// actually needs — who changed what, when — is kept by the activity log, which records every
/// save, so nothing is lost by not keeping every revision as a separate row.
/// </summary>
public static class NoteRules
{
    public const int MaxLength = 2000;

    public static ValidationResult Validate(string? body)
    {
        ValidationResult result = new();

        if (body is { Length: > MaxLength })
        {
            result.Add("body", $"Not en fazla {MaxLength} karakter olabilir.");
        }

        return result;
    }

    /// <summary>
    /// An empty (or whitespace-only) body means "clear the note". Returning null here is what
    /// lets the endpoint treat clearing and saving as the same upsert instead of needing a
    /// separate delete verb the UI would have to decide between.
    /// </summary>
    public static string? Normalize(string? body)
    {
        string trimmed = body?.Trim() ?? string.Empty;

        return trimmed.Length == 0 ? null : trimmed;
    }
}
