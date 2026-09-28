using System.Text.Json;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Features.Applications.GetActivity;

public sealed class GetActivityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("applications/{applicationUid}/activity", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("GetApplicationActivity")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string applicationUid,
        int? limit,
        HttpContext http,
        CompanyContextResolver resolver,
        RecruitingDbContext db,
        IApplicationReadStore readStore,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (await readStore.FindAsync(applicationUid, company!.CompanyUid, cancellationToken) is null)
        {
            return ApiResults.NotFound();
        }

        var entries = await db.Activity
            .AsNoTracking()
            .Where(a => a.ApplicationUid == applicationUid && a.CompanyUid == company.CompanyUid)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(limit ?? 50, 1, 200))
            .Select(a => new
            {
                a.Id,
                a.Type,
                a.ActorUid,
                a.ActorName,
                a.Metadata,
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // metadata is jsonb; handing it back as a string would make every client parse JSON twice.
        return Results.Ok(entries.Select(a => new ActivityResponse(
            a.Id,
            a.Type,
            a.ActorUid,
            a.ActorName,
            JsonDocument.Parse(string.IsNullOrWhiteSpace(a.Metadata) ? "{}" : a.Metadata).RootElement,
            a.CreatedAt)));
    }
}

public sealed record ActivityResponse(
    long Id, string Type, string? ActorUid, string? ActorName, JsonElement Metadata, DateTimeOffset CreatedAt);
