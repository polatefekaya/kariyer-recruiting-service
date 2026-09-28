using Kariyer.Recruiting.Domain.Pipeline;

namespace Kariyer.Recruiting.Domain.Applications;

/// <summary>
/// Derives the opening stage from the legacy <c>job_application.application_status</c> the first
/// time the pipeline sees an application. Read-only: the legacy column is never written back.
/// </summary>
public static class LegacyStageMapping
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pending"] = ApplicationStage.New,
        ["under_review"] = ApplicationStage.Reviewing,
        ["accepted"] = ApplicationStage.Hired,
        ["rejected"] = ApplicationStage.Rejected,
        ["withdrawn"] = ApplicationStage.Withdrawn,
    };

    public static string ToStage(string? legacyStatus) =>
        legacyStatus is not null && Map.TryGetValue(legacyStatus, out string? stage) ? stage : ApplicationStage.New;
}
