using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Applications.ListApplications;

public sealed class ListApplicationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("jobs/{jobUid}/applications", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListApplications")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        [AsParameters] ListApplicationsRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        IApplicationReadStore store,
        ICacheStore cache,
        CacheInvalidator invalidator,
        RecruitingMetrics metrics,
        IOptions<RecruitingOptions> recruiting,
        IOptions<GarnetOptions> garnet,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (request.Status is not null && !ApplicationStage.IsValid(request.Status))
        {
            return ApiResults.Validation("status", $"Durum şunlardan biri olmalı: {string.Join(", ", ApplicationStage.All)}.");
        }

        if (!await store.JobBelongsToCompanyAsync(jobUid, company!.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        ApplicationListQuery query = request.ToQuery(jobUid, company.CompanyUid, recruiting.Value);
        string cacheKey = CacheKeys.ApplicationList(garnet.Value.KeyPrefix, jobUid, Hash(query));

        long started = Stopwatch.GetTimestamp();

        ApplicationListResponse? cached = await cache.GetAsync<ApplicationListResponse>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            metrics.ListCompleted(Stopwatch.GetElapsedTime(started).TotalMilliseconds, cached: true);
            return Results.Ok(cached);
        }

        ApplicationPage page = await store.ListAsync(query, cancellationToken);

        ApplicationListResponse response = new(
            [.. page.Items.Select(item => item.ToResponse())],
            new PaginationResponse(
                query.Page,
                query.Limit,
                page.Total,
                (int)Math.Ceiling(page.Total / (double)query.Limit)),
            page.Stats);

        await cache.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromSeconds(garnet.Value.ApplicationListTtlSeconds),
            cancellationToken);

        metrics.ListCompleted(Stopwatch.GetElapsedTime(started).TotalMilliseconds, cached: false);

        return Results.Ok(response);
    }

    private static string Hash(ApplicationListQuery query)
    {
        string raw = string.Join(
            '|',
            query.Stage,
            query.Search,
            query.CandidateUid,
            query.AppliedFrom?.ToUnixTimeSeconds(),
            query.AppliedTo?.ToUnixTimeSeconds(),
            query.Sort,
            query.Page,
            query.Limit);

        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }
}

public sealed record ListApplicationsRequest
{
    [FromQuery] public string? Status { get; init; }

    [FromQuery(Name = "q")] public string? Search { get; init; }

    [FromQuery] public DateTimeOffset? AppliedFrom { get; init; }

    [FromQuery] public DateTimeOffset? AppliedTo { get; init; }

    [FromQuery] public string? Sort { get; init; }

    [FromQuery] public int? Page { get; init; }

    [FromQuery] public int? Limit { get; init; }

    public ApplicationListQuery ToQuery(string jobUid, string companyUid, RecruitingOptions options) => new()
    {
        JobUid = jobUid,
        CompanyUid = companyUid,
        Stage = Status,
        Search = Search,
        AppliedFrom = AppliedFrom,
        AppliedTo = AppliedTo,
        Sort = ParseSort(Sort),
        Page = Math.Max(Page ?? 1, 1),
        Limit = Math.Clamp(Limit ?? options.DefaultPageSize, 1, options.MaxPageSize),
    };

    private static ApplicationSort ParseSort(string? sort) => sort switch
    {
        "appliedAt:asc" => ApplicationSort.AppliedAtAsc,
        "score:desc" => ApplicationSort.ScoreDesc,
        "status:asc" or "stage:asc" => ApplicationSort.StageAsc,
        _ => ApplicationSort.AppliedAtDesc,
    };
}
