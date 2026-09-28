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
    IApplicationPipelineRepository pipelines,
    IActivityWriter activity,
    IIntegrationEventPublisher publisher,
    ICompanyDirectory directory,
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

        DateTimeOffset now = clock.GetUtcNow();

        ApplicationPipeline? pipeline = await pipelines.FindAsync(applicationUid, cancellationToken);

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

        StageChanged change;

        try
        {
            change = pipeline.MoveTo(target, company.UserUid, request.Reason, now);
        }
        catch (InvalidStageTransitionException exception)
        {
            return ApiResults.InvalidTransition(exception);
        }

        activity.Write(ActivityEntry.Create(
            summary.ApplicationUid,
            summary.JobUid,
            summary.CompanyUid,
            ActivityType.StageChanged,
            company.UserUid,
            company.UserName,
            JsonSerializer.Serialize(new { from = change.FromStage, to = change.ToStage, reason = change.Reason }),
            now));

        await publisher.PublishAsync(
            new ApplicationStageChangedEvent
            {
                MessageId = $"{summary.ApplicationUid}:{change.ToStage}:{now.ToUnixTimeMilliseconds()}",
                ApplicationUid = summary.ApplicationUid,
                JobUid = summary.JobUid,
                JobTitle = summary.JobTitle,
                CandidateUid = summary.CandidateUid,
                CompanyUid = summary.CompanyUid,
                FromStage = change.FromStage,
                ToStage = change.ToStage,
                ActorUid = company.UserUid,
                Reason = change.Reason ?? string.Empty,
                ChangedAt = now,
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateJobAsync(summary.JobUid, cancellationToken);

        metrics.StageChanged(change.FromStage, change.ToStage);

        return Results.Ok(new ChangeStageResponse(
            summary.ApplicationUid,
            pipeline.Stage,
            ApplicationStage.Label(pipeline.Stage),
            StageTransitions.From(pipeline.Stage)));
    }
}
