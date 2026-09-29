using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Api.Features.Applications.ListJobNotes;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Features.Applications.GetApplicationNote;

/// <summary>
/// One application's note. The job-wide listing exists for the applicant list, which shows a whole
/// posting at once; a screen about a single person should not have to pull every note on the job
/// to find theirs.
/// </summary>
public sealed class GetApplicationNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("applications/{applicationUid}/note", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("GetApplicationNote")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string applicationUid,
        HttpContext http,
        CompanyContextResolver resolver,
        GetApplicationNoteHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(applicationUid, company!, cancellationToken);
    }
}

public sealed class GetApplicationNoteHandler(
    RecruitingDbContext db,
    IApplicationReadStore readStore,
    ICompanyDirectory directory)
{
    public async Task<IResult> HandleAsync(
        string applicationUid, CompanyContext company, CancellationToken cancellationToken)
    {
        ApplicationSummary? summary = await readStore.FindAsync(applicationUid, company.CompanyUid, cancellationToken);

        if (summary is null)
        {
            return ApiResults.NotFound();
        }

        var note = await db.Notes
            .AsNoTracking()
            .Where(n => n.ApplicationUid == applicationUid && n.CompanyUid == company.CompanyUid)
            .Select(n => new
            {
                n.ApplicationUid,
                n.CandidateUid,
                n.Body,
                n.AuthorUid,
                n.CreatedAt,
                n.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
        {
            return Results.NoContent();
        }

        CompanyMember? author = (await directory.ListMembersAsync(company.CompanyUid, cancellationToken))
            .FirstOrDefault(m => string.Equals(m.Uid, note.AuthorUid, StringComparison.OrdinalIgnoreCase) ||
                                (m.ExternalId != null && string.Equals(m.ExternalId, note.AuthorUid, StringComparison.OrdinalIgnoreCase)));

        return Results.Ok(new JobNoteResponse(
            note.ApplicationUid,
            note.CandidateUid,
            note.Body,
            author is null
                ? new HiringAuthorResponse(note.AuthorUid, note.AuthorUid, null, null)
                : new HiringAuthorResponse(author.Uid, author.Name, author.Position, author.PhotoUrl),
            note.CreatedAt,
            note.UpdatedAt));
    }
}
