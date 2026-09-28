using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace Kariyer.Recruiting.Api.Features.Interviews.ListCandidateInterviews;

/// <summary>One candidate's interviews with THIS company only — never another employer's.</summary>
public sealed class ListCandidateInterviewsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("candidates/{candidateUid}/interviews", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListCandidateInterviews")
            .WithTags("Interviews");

    private static async Task<IResult> HandleAsync(
        string candidateUid,
        HttpContext http,
        CompanyContextResolver resolver,
        RecruitingDbContext db,
        ICompanyDirectory directory,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        List<Interview> interviews = await db.Interviews
            .AsNoTracking()
            .Where(i => i.CandidateUid == candidateUid && i.CompanyUid == company!.CompanyUid)
            .OrderByDescending(i => i.StartsAt)
            .ToListAsync(cancellationToken);

        Dictionary<string, CompanyMember> members =
            (await directory.ListMembersAsync(company!.CompanyUid, cancellationToken))
            .ToDictionary(m => m.Uid, StringComparer.Ordinal);

        return Results.Ok(interviews.Select(i => i.ToDetail(members)));
    }
}
