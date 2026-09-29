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

        return members.FirstOrDefault(m =>
            string.Equals(m.Uid, userUid, StringComparison.OrdinalIgnoreCase) ||
            (m.ExternalId != null && string.Equals(m.ExternalId, userUid, StringComparison.OrdinalIgnoreCase)));
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

        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.Uid == companyUid)
            .FirstOrDefaultAsync(cancellationToken);

        var list = new List<CompanyMember>();

        if (company is not null)
        {
            string ownerName = $"{company.AuthorizedName} {company.AuthorizedSurname}".Trim();
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                ownerName = company.CompanyName ?? "Şirket Yöneticisi";
            }

            list.Add(new CompanyMember(
                company.Uid,
                ownerName,
                "Şirket Yöneticisi",
                company.Email,
                company.PhotoUrl,
                company.ExternalId?.ToString()));
        }

        var employeeMembers = await (
            from link in db.CompanyEmployees.AsNoTracking()
            join employee in db.Employees.AsNoTracking() on link.EmployeeUid equals employee.Uid
            where link.CompanyUid == companyUid && link.IsActive && link.Status == "approved"
            select new
            {
                employee.Uid,
                employee.ExternalId,
                Name = ((employee.Name ?? "") + " " + (employee.Surname ?? "")).Trim(),
                link.Position,
                employee.Email,
                employee.PhotoUrl
            }).ToArrayAsync(cancellationToken);

        foreach (var emp in employeeMembers)
        {
            list.Add(new CompanyMember(
                emp.Uid,
                string.IsNullOrWhiteSpace(emp.Name) ? (emp.Email ?? emp.Uid) : emp.Name,
                emp.Position ?? "Ekip Üyesi",
                emp.Email,
                emp.PhotoUrl,
                emp.ExternalId?.ToString()));
        }

        CompanyMember[] members = [.. list];

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
