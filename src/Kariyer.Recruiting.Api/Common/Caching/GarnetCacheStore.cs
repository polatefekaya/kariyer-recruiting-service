using System.Net;
using System.Text.Json;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Domain.Ports;
using StackExchange.Redis;

namespace Kariyer.Recruiting.Api.Common.Caching;

public sealed class GarnetCacheStore(
    IConnectionMultiplexer multiplexer,
    RecruitingMetrics metrics,
    ILogger<GarnetCacheStore> logger) : ICacheStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        try
        {
            RedisValue value = await multiplexer.GetDatabase().StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                metrics.CacheMiss(typeof(T).Name);
                return null;
            }

            metrics.CacheHit(typeof(T).Name);

            return JsonSerializer.Deserialize<T>((string)value!, Json);
        }
        catch (Exception ex) when (ex is RedisException or JsonException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await multiplexer.GetDatabase()
                .StringSetAsync(key, JsonSerializer.Serialize(value, Json), ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await multiplexer.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Cache invalidation failed for {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        try
        {
            IDatabase database = multiplexer.GetDatabase();

            foreach (EndPoint endpoint in multiplexer.GetEndPoints())
            {
                IServer server = multiplexer.GetServer(endpoint);

                if (server.IsReplica || !server.IsConnected)
                {
                    continue;
                }

                await foreach (RedisKey key in server.KeysAsync(pattern: $"{prefix}*", pageSize: 512)
                                   .WithCancellation(cancellationToken))
                {
                    await database.KeyDeleteAsync(key);
                }
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache prefix invalidation failed for {Prefix}", prefix);
        }
    }
}

public sealed class DisabledCacheStore : ICacheStore
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken) where T : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken) => Task.CompletedTask;
}
