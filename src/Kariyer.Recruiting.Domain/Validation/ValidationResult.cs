namespace Kariyer.Recruiting.Domain.Validation;

/// <summary>
/// Field-keyed validation errors, shaped for the <c>VALIDATION_ERROR</c> response the technical
/// document defines (§5.1). Collected rather than thrown one at a time so a form comes back with
/// everything that is wrong with it, not with whichever field happened to be checked first.
/// </summary>
public sealed class ValidationResult
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyDictionary<string, string[]> Errors =>
        _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out List<string>? messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }
}
