using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Kariyer.Recruiting.Api.Common.Caching;

public static class CachingExtensions
{
    public static IServiceCollection AddGarnetCache(this IServiceCollection services, IConfiguration configuration)
    {
        GarnetOptions garnet = new();
        configuration.GetSection(GarnetOptions.SectionName).Bind(garnet);

        if (!garnet.Enabled || string.IsNullOrWhiteSpace(garnet.ConnectionString))
        {
            services.AddSingleton<ICacheStore, DisabledCacheStore>();
            return services;
        }

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            GarnetOptions options = sp.GetRequiredService<IOptions<GarnetOptions>>().Value;
            ConfigurationOptions config = ConfigurationOptions.Parse(options.ConnectionString);

            // A cache that is down degrades latency, not correctness: never block boot on it.
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = 5_000;
            config.SyncTimeout = 5_000;

            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<ICacheStore, GarnetCacheStore>();

        return services;
    }
}
