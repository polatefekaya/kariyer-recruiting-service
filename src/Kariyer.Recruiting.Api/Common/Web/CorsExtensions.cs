using Kariyer.Recruiting.Api.Common.Configuration;

namespace Kariyer.Recruiting.Api.Common.Web;

public static class CorsExtensions
{
    public const string PolicyName = "recruiting:portal";

    public static IServiceCollection AddPortalCors(this IServiceCollection services, IConfiguration configuration)
    {
        string[] origins = configuration
            .GetSection($"{RecruitingOptions.SectionName}:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));

        return services;
    }
}
