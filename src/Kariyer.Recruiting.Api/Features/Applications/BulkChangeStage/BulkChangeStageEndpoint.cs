using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Api.Features.Applications.ChangeStage;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Applications.BulkChangeStage;

public sealed class BulkChangeStageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("applications/status", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("BulkChangeApplicationStage")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        BulkChangeStageRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        BulkChangeStageHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(request, company!, cancellationToken);
    }
}

public sealed record BulkChangeStageRequest(IReadOnlyList<string>? ApplicationUids, string Status, string? Reason);

/// <summary>Why one application of a bulk move stayed where it was.</summary>
public sealed record BulkStageSkip(string ApplicationUid, string Reason, string? Stage);

public sealed record BulkChangeStageResponse(
    IReadOnlyList<ChangeStageResponse> Moved,
    IReadOnlyList<BulkStageSkip> Skipped);

/// <summary>
/// The same move for many applications at once. Each application is judged on its own: one that
/// cannot make the move (another company's, already there, a transition the pipeline forbids) is
/// reported back and the rest still move — a selection that spans stages is the normal case, not
/// an error. Everything that moves commits together.
/// </summary>
public sealed class BulkChangeStageHandler(
    IApplicationReadStore readStore,
    StageMoveHandler mover,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    RecruitingMetrics metrics,
    TimeProvider clock)
{
    /// <summary>One request's ceiling; the portal pages its list well below this.</summary>
    public const int MaxApplications = 200;

    public const string NotFound = "NOT_FOUND";

    public const string InvalidTransition = "INVALID_STATUS_TRANSITION";

    public async Task<IResult> HandleAsync(
        BulkChangeStageRequest request,
        CompanyContext company,
        CancellationToken cancellationToken)
    {
        if (!ApplicationStage.TryParse(request.Status, out string target))
        {
            return ApiResults.Validation("status", $"Durum şunlardan biri olmalı: {string.Join(", ", ApplicationStage.All)}.");
        }

        if (target == ApplicationStage.Withdrawn)
        {
            return ApiResults.Forbidden("Başvuruyu yalnızca aday geri çekebilir.");
        }

        string[] uids = [.. (request.ApplicationUids ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal)];

        if (uids.Length == 0)
        {
            return ApiResults.Validation("applicationUids", "En az bir başvuru seçin.");
        }

        if (uids.Length > MaxApplications)
        {
            return ApiResults.Validation("applicationUids", $"Tek seferde en fazla {MaxApplications} başvuru taşınabilir.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        List<ChangeStageResponse> moved = [];
        List<BulkStageSkip> skipped = [];
        List<StageMove> moves = [];
        HashSet<string> jobs = new(StringComparer.Ordinal);

        foreach (string uid in uids)
        {
            ApplicationSummary? summary = await readStore.FindAsync(uid, company.CompanyUid, cancellationToken);

            if (summary is null)
            {
                skipped.Add(new BulkStageSkip(uid, NotFound, null));
                continue;
            }

            try
            {
                StageMove move = await mover.MoveAsync(summary, target, request.Reason, company, now, cancellationToken);

                moves.Add(move);
                jobs.Add(summary.JobUid);
                moved.Add(new ChangeStageResponse(
                    uid,
                    move.Pipeline.Stage,
                    ApplicationStage.Label(move.Pipeline.Stage),
                    StageTransitions.From(move.Pipeline.Stage)));
            }
            catch (InvalidStageTransitionException exception)
            {
                skipped.Add(new BulkStageSkip(uid, InvalidTransition, exception.From));
            }
        }

        if (moves.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (string job in jobs)
            {
                await cache.InvalidateJobAsync(job, cancellationToken);
            }

            foreach (StageMove move in moves)
            {
                metrics.StageChanged(move.Change.FromStage, move.Change.ToStage);
            }
        }

        return Results.Ok(new BulkChangeStageResponse(moved, skipped));
    }
}
