namespace Kariyer.Recruiting.Api.Common.Persistence.Projections;

/// <summary>
/// Keyless projections over tables the Node monolith owns. Mapped to the columns this service
/// reads and excluded from migrations: nothing here is ever created, altered or written.
/// </summary>
public sealed record JobApplicationProjection
{
    public string Uid { get; init; } = string.Empty;

    public string JobUid { get; init; } = string.Empty;

    public string ApplicantUid { get; init; } = string.Empty;

    public int? ResumeId { get; init; }

    public string ApplicationStatus { get; init; } = string.Empty;

    public DateTimeOffset AppliedAt { get; init; }
}

public sealed record CompanyJobProjection
{
    public string Uid { get; init; } = string.Empty;

    public string? CompanyUid { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Department { get; init; } = string.Empty;

    public string Position { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string Town { get; init; } = string.Empty;
}

public sealed record EmployeeProjection
{
    public string Uid { get; init; } = string.Empty;

    public Guid? ExternalId { get; init; }

    public string? Username { get; init; }

    public string? Name { get; init; }

    public string? Surname { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? PhotoUrl { get; init; }

    public string? Province { get; init; }

    public string? Town { get; init; }
}

public sealed record CompanyEmployeeProjection
{
    public string CompanyUid { get; init; } = string.Empty;

    public string EmployeeUid { get; init; } = string.Empty;

    public string? Position { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed record CompanyProjection
{
    public string Uid { get; init; } = string.Empty;

    public Guid? ExternalId { get; init; }

    public string? CompanyName { get; init; }

    public string? AuthorizedName { get; init; }

    public string? AuthorizedSurname { get; init; }

    public string? Email { get; init; }

    public string? PhotoUrl { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsAccountCompleted { get; init; }
}
