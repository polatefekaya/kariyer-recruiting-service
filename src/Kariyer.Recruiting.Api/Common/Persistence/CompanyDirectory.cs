using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Persistence;

public sealed class CompanyDirectory(
    RecruitingDbContext db,
    ICacheStore cache,
    IOptions<GarnetOptions> options) : ICompanyDirectory
{
    public async Task<CompanyMember?> FindMemberAsync(
        string companyUid, string userUid, CancellationToken cancellationToken)
    {
        IReadOnlyList<CompanyMember> members = await ListMembersAsync(companyUid, cancellationToken);

        return members.FirstOrDefault(m => string.Equals(m.Uid, userUid, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<CompanyMember>> ListMembersAsync(
        string companyUid, CancellationToken cancellationToken)
    {
        string key = CacheKeys.CompanyMembers(options.Value.KeyPrefix, companyUid);

        CompanyMember[]? cached = await cache.GetAsync<CompanyMember[]>(key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        CompanyMember[] members = await (
            from link in db.CompanyEmployees.AsNoTracking()
            join employee in db.Employees.AsNoTracking() on link.EmployeeUid equals employee.Uid
            where link.CompanyUid == companyUid && link.IsActive && link.Status == "approved"
            select new CompanyMember(
                employee.Uid,
                ((employee.Name ?? "") + " " + (employee.Surname ?? "")).Trim(),
                link.Position,
                employee.Email,
                employee.PhotoUrl)).ToArrayAsync(cancellationToken);

        await cache.SetAsync(
            key, members, TimeSpan.FromSeconds(options.Value.DirectoryTtlSeconds), cancellationToken);

        return members;
    }

    public async Task<string?> FindCompanyNameAsync(string companyUid, CancellationToken cancellationToken)
    {
        string key = CacheKeys.CompanyName(options.Value.KeyPrefix, companyUid);

        CachedName? cached = await cache.GetAsync<CachedName>(key, cancellationToken);

        if (cached is not null)
        {
            return cached.Value;
        }

        string? name = await db.Companies
            .AsNoTracking()
            .Where(c => c.Uid == companyUid)
            .Select(c => c.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

        await cache.SetAsync(
            key, new CachedName(name), TimeSpan.FromSeconds(options.Value.DirectoryTtlSeconds), cancellationToken);

        return name;
    }

    private sealed record CachedName(string? Value);
}
