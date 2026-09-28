namespace Kariyer.Recruiting.Domain.SavedFilters;

public sealed class SavedFilter
{
    private SavedFilter()
    {
    }

    public Guid Id { get; private set; }

    public string CompanyUid { get; private set; } = string.Empty;

    public string UserUid { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Query { get; private set; } = "{}";

    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static SavedFilter Create(
        string companyUid, string userUid, string name, string query, bool isDefault, DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            CompanyUid = companyUid,
            UserUid = userUid,
            Name = name.Trim(),
            Query = query,
            IsDefault = isDefault,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Rename(string name, DateTimeOffset now)
    {
        Name = name.Trim();
        UpdatedAt = now;
    }

    public void ClearDefault(DateTimeOffset now)
    {
        IsDefault = false;
        UpdatedAt = now;
    }
}
