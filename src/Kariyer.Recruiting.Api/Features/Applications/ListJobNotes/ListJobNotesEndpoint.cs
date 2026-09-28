using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Features.Applications.ListJobNotes;

public sealed class ListJobNotesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("jobs/{jobUid}/notes", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListJobNotes")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        HttpContext http,
        CompanyContextResolver resolver,
        ListJobNotesHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(jobUid, company!, cancellationToken);
    }
}

public sealed record JobNoteResponse(
    string ApplicationUid,
    string CandidateUid,
    string Body,
    HiringAuthorResponse Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HiringAuthorResponse(string Uid, string Name, string? Position, string? PhotoUrl);

public sealed class ListJobNotesHandler(
    RecruitingDbContext db,
    IApplicationReadStore readStore,
    ICompanyDirectory directory)
{
    public async Task<IResult> HandleAsync(string jobUid, CompanyContext company, CancellationToken cancellationToken)
    {
        if (!await readStore.JobBelongsToCompanyAsync(jobUid, company.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.JobUid == jobUid && n.CompanyUid == company.CompanyUid)
            .Select(n => new
            {
                n.ApplicationUid,
                n.CandidateUid,
                n.Body,
                n.AuthorUid,
                n.CreatedAt,
                n.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        Dictionary<string, CompanyMember> members =
            (await directory.ListMembersAsync(company.CompanyUid, cancellationToken))
            .ToDictionary(m => m.Uid, StringComparer.Ordinal);

        return Results.Ok(notes.Select(n => new JobNoteResponse(
            n.ApplicationUid,
            n.CandidateUid,
            n.Body,
            members.TryGetValue(n.AuthorUid, out CompanyMember? author)
                ? new HiringAuthorResponse(author.Uid, author.Name, author.Position, author.PhotoUrl)
                : new HiringAuthorResponse(n.AuthorUid, n.AuthorUid, null, null),
            n.CreatedAt,
            n.UpdatedAt)));
    }
}
