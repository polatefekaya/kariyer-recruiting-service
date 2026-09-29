using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Interviews.ListJobInterviews;

public sealed class ListJobInterviewsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("jobs/{jobUid}/interviews", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListJobInterviews")
            .WithTags("Interviews");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        HttpContext http,
        CompanyContextResolver resolver,
        ListJobInterviewsHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(jobUid, company!, cancellationToken);
    }
}

public sealed record JobInterviewsResponse(
    IReadOnlyList<InterviewDetailResponse> Ongoing,
    IReadOnlyList<InterviewDetailResponse> Upcoming,
    IReadOnlyList<InterviewDetailResponse> Past);

public sealed class ListJobInterviewsHandler(
    IApplicationReadStore readStore,
    IInterviewRepository interviews,
    ICompanyDirectory directory,
    ICacheStore cache,
    IOptions<GarnetOptions> garnet,
    TimeProvider clock)
{
    public async Task<IResult> HandleAsync(string jobUid, CompanyContext company, CancellationToken cancellationToken)
    {
        if (!await readStore.JobBelongsToCompanyAsync(jobUid, company.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        string key = CacheKeys.Interviews(garnet.Value.KeyPrefix, jobUid);

        JobInterviewsResponse? cached = await cache.GetAsync<JobInterviewsResponse>(key, cancellationToken);

        if (cached is not null)
        {
            return Results.Ok(cached);
        }

        IReadOnlyList<Interview> rows = await interviews.ListByJobAsync(jobUid, cancellationToken);
        IReadOnlyDictionary<string, CompanyMember> members = await MembersAsync(company.CompanyUid, cancellationToken);

        DateTimeOffset now = clock.GetUtcNow();

        JobInterviewsResponse response = new(
            [.. rows.Where(i => i.IsOngoing(now)).Select(i => i.ToDetail(members))],
            [.. rows.Where(i => i.IsUpcoming(now)).OrderBy(i => i.StartsAt).Select(i => i.ToDetail(members))],
            [.. rows.Where(i => !i.IsOngoing(now) && !i.IsUpcoming(now))
                .OrderByDescending(i => i.StartsAt)
                .Select(i => i.ToDetail(members))]);

        await cache.SetAsync(key, response, TimeSpan.FromSeconds(garnet.Value.ApplicationListTtlSeconds), cancellationToken);

        return Results.Ok(response);
    }

    private async Task<IReadOnlyDictionary<string, CompanyMember>> MembersAsync(
        string companyUid, CancellationToken cancellationToken)
    {
        var memberList = await directory.ListMembersAsync(companyUid, cancellationToken);
        var members = new Dictionary<string, CompanyMember>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in memberList)
        {
            members[m.Uid] = m;
            if (!string.IsNullOrEmpty(m.ExternalId))
            {
                members[m.ExternalId] = m;
            }
        }
        return members;
    }
}
