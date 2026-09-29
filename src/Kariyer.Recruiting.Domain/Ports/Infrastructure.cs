namespace Kariyer.Recruiting.Domain.Ports;

public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>Drops every key under a prefix; used when a write invalidates a whole list.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);
}

public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class;
}

public interface ICompanyDirectory
{
    Task<CompanyMember?> FindMemberAsync(string companyUid, string userUid, CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyMember>> ListMembersAsync(string companyUid, CancellationToken cancellationToken);

    Task<string?> FindCompanyNameAsync(string companyUid, CancellationToken cancellationToken);
}

public sealed record CompanyMember(string Uid, string Name, string? Position, string? Email, string? PhotoUrl, string? ExternalId = null);
