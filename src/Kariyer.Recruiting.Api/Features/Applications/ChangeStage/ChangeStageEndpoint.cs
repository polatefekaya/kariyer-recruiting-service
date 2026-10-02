using System.Text.Json;
using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Applications.ChangeStage;

public sealed class ChangeStageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("applications/{applicationUid}/status", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("ChangeApplicationStage")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string applicationUid,
        ChangeStageRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        ChangeStageHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(applicationUid, request, company!, cancellationToken);
    }
}

public sealed record ChangeStageRequest(string Status, string? Reason);

public sealed record ChangeStageResponse(string ApplicationUid, string Stage, string StageLabel, IReadOnlyList<string> AllowedActions);

public sealed class ChangeStageHandler(
    IApplicationReadStore readStore,
    StageMoveHandler mover,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    RecruitingMetrics metrics,
    TimeProvider clock)
{
    public async Task<IResult> HandleAsync(
        string applicationUid,
        ChangeStageRequest request,
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

        ApplicationSummary? summary = await readStore.FindAsync(applicationUid, company.CompanyUid, cancellationToken);

        if (summary is null)
        {
            return ApiResults.NotFound();
        }

        StageMove move;

        try
        {
            move = await mover.MoveAsync(summary, target, request.Reason, company, clock.GetUtcNow(), cancellationToken);
        }
        catch (InvalidStageTransitionException exception)
        {
            return ApiResults.InvalidTransition(exception);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateJobAsync(summary.JobUid, cancellationToken);

        metrics.StageChanged(move.Change.FromStage, move.Change.ToStage);

        return Results.Ok(new ChangeStageResponse(
            summary.ApplicationUid,
            move.Pipeline.Stage,
            ApplicationStage.Label(move.Pipeline.Stage),
            StageTransitions.From(move.Pipeline.Stage)));
    }
}

public sealed record StageMove(ApplicationPipeline Pipeline, StageChanged Change);

/// <summary>
/// One pipeline move, staged but not saved: the pipeline row (started from the legacy status on
/// first touch), the activity entry and the outbox event. Shared by the single and the bulk
/// endpoint so a move means the same thing whichever way it was made; the caller commits.
/// </summary>
public sealed class StageMoveHandler(
    IApplicationPipelineRepository pipelines,
    IActivityWriter activity,
    IIntegrationEventPublisher publisher,
    ICompanyDirectory directory)
{
    private string? _companyName;

    /// <exception cref="InvalidStageTransitionException">The move is not one the pipeline allows.</exception>
    public async Task<StageMove> MoveAsync(
        ApplicationSummary summary,
        string target,
        string? reason,
        CompanyContext company,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ApplicationPipeline? pipeline = await pipelines.FindAsync(summary.ApplicationUid, cancellationToken);

        if (pipeline is null)
        {
            pipeline = ApplicationPipeline.Start(
                summary.ApplicationUid,
                summary.JobUid,
                summary.CompanyUid,
                summary.CandidateUid,
                LegacyStageMapping.ToStage(summary.LegacyStatus),
                now);

            pipelines.Add(pipeline);
        }

        StageChanged change = pipeline.MoveTo(target, company.UserUid, reason, now);

        activity.Write(ActivityEntry.Create(
            summary.ApplicationUid,
            summary.JobUid,
            summary.CompanyUid,
            ActivityType.StageChanged,
            company.UserUid,
            company.UserName,
            JsonSerializer.Serialize(new { from = change.FromStage, to = change.ToStage, reason = change.Reason }),
            now));

        // The mail service tells the candidate about OFFER / HIRED / REJECTED from this event alone —
        // it has no candidate or company table — so the recipient travels with the move.
        _companyName ??= await directory.FindCompanyNameAsync(summary.CompanyUid, cancellationToken) ?? string.Empty;

        await publisher.PublishAsync(
            new ApplicationStageChangedEvent
            {
                MessageId = $"{summary.ApplicationUid}:{change.ToStage}:{now.ToUnixTimeMilliseconds()}",
                ApplicationUid = summary.ApplicationUid,
                JobUid = summary.JobUid,
                JobTitle = summary.JobTitle,
                CandidateUid = summary.CandidateUid,
                CandidateEmail = summary.CandidateEmail ?? string.Empty,
                CandidateName = $"{summary.CandidateName} {summary.CandidateSurname}".Trim(),
                CompanyUid = summary.CompanyUid,
                CompanyName = _companyName,
                FromStage = change.FromStage,
                ToStage = change.ToStage,
                ActorUid = company.UserUid,
                Reason = change.Reason ?? string.Empty,
                ChangedAt = now,
            },
            cancellationToken);

        return new StageMove(pipeline, change);
    }
}
