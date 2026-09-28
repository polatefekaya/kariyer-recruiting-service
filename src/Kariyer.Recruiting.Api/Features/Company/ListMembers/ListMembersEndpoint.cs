using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Ports;

namespace Kariyer.Recruiting.Api.Features.Company.ListMembers;

public sealed class ListMembersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("company/members", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .WithName("ListCompanyMembers")
            .WithTags("Company");

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        CompanyContextResolver resolver,
        ICompanyDirectory directory,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        IReadOnlyList<CompanyMember> members = await directory.ListMembersAsync(company!.CompanyUid, cancellationToken);

        // The signed-in user always appears, even when the company_employee link is missing:
        // they must be selectable as the interviewer of their own invitation.
        if (!members.Any(m => string.Equals(m.Uid, company.UserUid, StringComparison.Ordinal)))
        {
            members = [new CompanyMember(company.UserUid, company.UserName, null, null, null), .. members];
        }

        return Results.Ok(members.Select(m => new
        {
            uid = m.Uid,
            name = m.Name,
            position = m.Position,
            photo_url = m.PhotoUrl,
        }));
    }
}
