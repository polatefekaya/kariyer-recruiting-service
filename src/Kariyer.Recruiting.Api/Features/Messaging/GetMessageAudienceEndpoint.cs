using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Messaging;

/// <summary>
/// Who a message would reach: the posting's applicants in the chosen stages, each with whether —
/// and when — they were already messaged, so the portal can flag them and let the sender leave
/// them out. Read live, not cached: the answer changes the moment a message is sent.
/// </summary>
public sealed class GetMessageAudienceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("jobs/{jobUid}/message-audience", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("GetMessageAudience")
            .WithTags("Messaging");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        string? stages,
        HttpContext http,
        CompanyContextResolver resolver,
        IApplicationReadStore store,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        List<string> parsed = [];

        foreach (string value in (stages ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ApplicationStage.TryParse(value, out string stage))
            {
                return ApiResults.Validation("stages", $"Durum şunlardan biri olmalı: {string.Join(", ", ApplicationStage.All)}.");
            }

            parsed.Add(stage);
        }

        if (!await store.JobBelongsToCompanyAsync(jobUid, company!.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        IReadOnlyList<MessageAudienceRow> rows =
            await store.AudienceAsync(jobUid, company.CompanyUid, parsed, null, cancellationToken);

        return Results.Ok(rows.Select(r => r.ToResponse()).ToList());
    }
}
