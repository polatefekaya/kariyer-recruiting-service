using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Applications.ListCompanyApplications;

/// <summary>
/// Every application across the company's postings — the Adaylar screen, which folds them per
/// candidate client-side. Not cached: it is paged through continuously and its filters vary per
/// keystroke, so a cache would mostly serve entries nobody asks for twice.
/// </summary>
public sealed class ListCompanyApplicationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("company/applications", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListCompanyApplications")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        [AsParameters] ListCompanyApplicationsRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        IApplicationReadStore store,
        IOptions<RecruitingOptions> options,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (request.Status is not null && !ApplicationStage.IsValid(request.Status))
        {
            return ApiResults.Validation(
                "status", $"Durum şunlardan biri olmalı: {string.Join(", ", ApplicationStage.All)}.");
        }

        ApplicationListQuery query = new()
        {
            JobUid = request.JobUid,
            CompanyUid = company!.CompanyUid,
            CandidateUid = request.CandidateUid,
            Stage = request.Status,
            Search = request.Search,
            Sort = request.Sort == "appliedAt:asc" ? ApplicationSort.AppliedAtAsc : ApplicationSort.AppliedAtDesc,
            Page = Math.Max(request.Page ?? 1, 1),
            Limit = Math.Clamp(request.Limit ?? options.Value.DefaultPageSize, 1, options.Value.MaxPageSize),
        };

        ApplicationPage page = await store.ListAsync(query, cancellationToken);

        return Results.Ok(new ApplicationListResponse(
            [.. page.Items.Select(item => item.ToResponse())],
            new PaginationResponse(
                query.Page, query.Limit, page.Total, (int)Math.Ceiling(page.Total / (double)query.Limit)),
            page.Stats));
    }
}

public sealed record ListCompanyApplicationsRequest
{
    [FromQuery] public string? JobUid { get; init; }

    [FromQuery] public string? CandidateUid { get; init; }

    [FromQuery] public string? Status { get; init; }

    [FromQuery(Name = "q")] public string? Search { get; init; }

    [FromQuery] public string? Sort { get; init; }

    [FromQuery] public int? Page { get; init; }

    [FromQuery] public int? Limit { get; init; }
}
