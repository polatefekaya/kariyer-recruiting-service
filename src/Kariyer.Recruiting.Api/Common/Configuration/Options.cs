using System.ComponentModel.DataAnnotations;

namespace Kariyer.Recruiting.Api.Common.Configuration;

public sealed class RecruitingOptions
{
    public const string SectionName = "Recruiting";

    [Range(1, 100)]
    public int MaxPageSize { get; init; } = 100;

    [Range(1, 100)]
    public int DefaultPageSize { get; init; } = 20;

    /// <summary>Base URL of the candidate-facing site, used in e-mails.</summary>
    public string PublicSiteUrl { get; init; } = "https://kariyerzamani.com";

    /// <summary>
    /// Base URL of the employer portal (kariyer-basvuru-web), used for the links in mails that go
    /// to the company side — e.g. a candidate's answer to an interview invitation.
    /// </summary>
    public string EmployerPortalUrl { get; init; } = "https://basvurular.kariyerzamani.com";

    /// <summary>
    /// Public base URL of this service. The accept/decline links resolve here: the confirmation
    /// page is served by the service itself so the link works in any mail client, with no session.
    /// </summary>
    public string PublicApiUrl { get; init; } = "http://localhost:5340/api/recruiting";

    /// <summary>
    /// HMAC key for the candidate's one-click accept/decline links. Must be set wherever those
    /// links are actually sent; the default only exists so a developer machine boots.
    /// </summary>
    public string ConfirmationSigningKey { get; init; } = DevelopmentSigningKey;

    public const string DevelopmentSigningKey = "development-only-signing-key";

    /// <summary>How long an invitation link stays answerable.</summary>
    public TimeSpan ConfirmationLinkLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Browser origins allowed to call this service. The employer portal is a separate origin,
    /// so without this every request from it is blocked before it arrives.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}

/// <summary>Rate limits for the mutating routes; see <c>RateLimitExtensions</c>.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    /// <summary>Writes per minute per signed-in user. Generous: a recruiter working a list moves fast.</summary>
    [Range(1, 10_000)]
    public int WritesPerMinute { get; init; } = 60;

    /// <summary>Answers per minute for one interview's accept/decline routes, which are anonymous.</summary>
    [Range(1, 10_000)]
    public int ConfirmationsPerMinute { get; init; } = 10;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string ExternalProviderUrl { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = "authenticated";

    public DevIdentityOptions DevIdentity { get; init; } = new();
}

/// <summary>Local-development bypass; see <c>DevelopmentIdentityHandler</c>.</summary>
public sealed class DevIdentityOptions
{
    public bool Enabled { get; init; }

    /// <summary>Matched against `company.external_id` (or `company.uid`).</summary>
    public string ExternalId { get; init; } = string.Empty;
}

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool MigrateOnStartup { get; init; }

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}

public sealed class GarnetOptions
{
    public const string SectionName = "Garnet";

    public string ConnectionString { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    [Range(1, 3600)]
    public int ApplicationListTtlSeconds { get; init; } = 60;

    [Range(1, 3600)]
    public int StatsTtlSeconds { get; init; } = 120;

    [Range(1, 86400)]
    public int DirectoryTtlSeconds { get; init; } = 900;

    public string KeyPrefix { get; init; } = "recruiting";
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; init; } = true;
}
