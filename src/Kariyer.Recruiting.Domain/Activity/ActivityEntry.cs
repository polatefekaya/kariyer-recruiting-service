namespace Kariyer.Recruiting.Domain.Activity;

public sealed class ActivityEntry
{
    private ActivityEntry()
    {
    }

    public long Id { get; private set; }

    public string ApplicationUid { get; private set; } = string.Empty;

    public string JobUid { get; private set; } = string.Empty;

    public string CompanyUid { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public string? ActorUid { get; private set; }

    public string? ActorName { get; private set; }

    public string Metadata { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public static ActivityEntry Create(
        string applicationUid,
        string jobUid,
        string companyUid,
        string type,
        string? actorUid,
        string? actorName,
        string metadataJson,
        DateTimeOffset now)
    {
        if (!ActivityType.IsValid(type))
        {
            throw new ArgumentException($"Unknown activity type: {type}", nameof(type));
        }

        return new ActivityEntry
        {
            ApplicationUid = applicationUid,
            JobUid = jobUid,
            CompanyUid = companyUid,
            Type = type,
            ActorUid = actorUid,
            ActorName = actorName,
            Metadata = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson,
            CreatedAt = now,
        };
    }
}
