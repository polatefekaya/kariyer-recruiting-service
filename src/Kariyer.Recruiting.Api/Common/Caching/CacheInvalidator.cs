using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Caching;

/// <summary>
/// Every write funnels its invalidation through here, so a new cached read only has to be
/// registered in one place to be dropped correctly.
/// </summary>
public sealed class CacheInvalidator(ICacheStore cache, IOptions<GarnetOptions> options)
{
    private readonly string _prefix = options.Value.KeyPrefix;

    public Task InvalidateJobAsync(string jobUid, CancellationToken cancellationToken) =>
        cache.RemoveByPrefixAsync(CacheKeys.JobScope(_prefix, jobUid), cancellationToken);

    public Task InvalidateInterviewsAsync(string jobUid, CancellationToken cancellationToken) =>
        Task.WhenAll(
            cache.RemoveAsync(CacheKeys.Interviews(_prefix, jobUid), cancellationToken),
            cache.RemoveByPrefixAsync($"{CacheKeys.JobScope(_prefix, jobUid)}:apps:", cancellationToken));

    public Task InvalidateCompanyDirectoryAsync(string companyUid, CancellationToken cancellationToken) =>
        cache.RemoveAsync(CacheKeys.CompanyMembers(_prefix, companyUid), cancellationToken);
}
