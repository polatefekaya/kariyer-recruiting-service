using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Applications.GetApplicationStats;

/// <summary>Chip counts plus the interview total the İlan detayı header shows.</summary>
public sealed class GetApplicationStatsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("jobs/{jobUid}/application-stats", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("GetApplicationStats")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        HttpContext http,
        CompanyContextResolver resolver,
        IApplicationReadStore store,
        RecruitingDbContext db,
        ICacheStore cache,
        IOptions<GarnetOptions> garnet,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!await store.JobBelongsToCompanyAsync(jobUid, company!.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        string key = CacheKeys.Stats(garnet.Value.KeyPrefix, jobUid);

        ApplicationStatsResponse? cached = await cache.GetAsync<ApplicationStatsResponse>(key, cancellationToken);

        if (cached is not null)
        {
            return Results.Ok(cached);
        }

        IReadOnlyDictionary<string, int> stages = await store.StatsAsync(jobUid, company.CompanyUid, cancellationToken);

        int scheduled = await db.Interviews
            .AsNoTracking()
            .CountAsync(i => i.JobUid == jobUid && i.Status == InterviewStatus.Scheduled, cancellationToken);

        int total = await db.Interviews.AsNoTracking().CountAsync(i => i.JobUid == jobUid, cancellationToken);

        ApplicationStatsResponse response = new(stages, new InterviewStatsResponse(total, scheduled));

        await cache.SetAsync(key, response, TimeSpan.FromSeconds(garnet.Value.StatsTtlSeconds), cancellationToken);

        return Results.Ok(response);
    }
}

public sealed record ApplicationStatsResponse(
    IReadOnlyDictionary<string, int> Stages, InterviewStatsResponse Interviews);

public sealed record InterviewStatsResponse(int Total, int Scheduled);
