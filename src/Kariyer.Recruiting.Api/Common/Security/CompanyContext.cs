using System.Security.Claims;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Security;

public sealed record CompanyContext(string CompanyUid, string UserUid, string UserName);

/// <summary>
/// Resolves the Supabase token into the company whose pipeline the caller may touch. Every
/// query in every feature is scoped by the resolved <c>CompanyUid</c>; nothing accepts a company
/// identifier from the request.
/// </summary>
public sealed class CompanyContextResolver(
    RecruitingDbContext db,
    ICacheStore cache,
    IOptions<GarnetOptions> garnet)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<(CompanyContext? Context, IResult? Failure)> ResolveAsync(
        HttpContext http, CancellationToken cancellationToken)
    {
        string? externalId = http.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return (null, ApiResults.Unauthorized());
        }

        string key = $"{garnet.Value.KeyPrefix}:identity:{externalId}";

        CompanyContext? cached = await cache.GetAsync<CompanyContext>(key, cancellationToken);

        if (cached is not null)
        {
            return (cached, null);
        }

        var query = db.Companies.AsNoTracking();

        if (Guid.TryParse(externalId, out Guid parsedGuid))
        {
            query = query.Where(c => c.ExternalId == parsedGuid || c.Uid == externalId);
        }
        else
        {
            query = query.Where(c => c.Uid == externalId);
        }

        var company = await query
            .Select(c => new {
                c.Uid,
                c.CompanyName,
                c.AuthorizedName,
                c.AuthorizedSurname,
                c.Status,
                c.IsAccountCompleted
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return (null, ApiResults.Forbidden("Bu panel şirket hesapları içindir."));
        }

        if (!company.IsAccountCompleted || !string.Equals(company.Status, "approved", StringComparison.Ordinal))
        {
            return (null, ApiResults.Forbidden("Şirket hesabınız onaylanmadan bu işlem yapılamaz."));
        }

        string displayName = $"{company.AuthorizedName} {company.AuthorizedSurname}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = company.CompanyName ?? company.Uid;
        }

        CompanyContext context = new(company.Uid, externalId, displayName);

        await cache.SetAsync(key, context, CacheTtl, cancellationToken);

        return (context, null);
    }
}
