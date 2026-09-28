using Kariyer.Recruiting.Api.Common.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kariyer.Recruiting.Api.Common.Security;

public static class AuthenticationExtensions
{
    public const string CompanyPolicy = "recruiting:company";

    public static IServiceCollection AddRecruitingAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        AuthOptions auth = new();
        configuration.GetSection(AuthOptions.SectionName).Bind(auth);

        if (auth.DevIdentity.Enabled)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Auth:DevIdentity:Enabled is only allowed when ASPNETCORE_ENVIRONMENT=Development.");
            }

            services
                .AddAuthentication(DevelopmentIdentityHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityHandler>(
                    DevelopmentIdentityHandler.SchemeName, _ => { });

            services.AddAuthorizationBuilder()
                .AddPolicy(CompanyPolicy, policy => policy.RequireAuthenticatedUser());

            services.AddScoped<CompanyContextResolver>();

            return services;
        }

        string authority = $"{auth.ExternalProviderUrl.TrimEnd('/')}/auth/v1";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = authority;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(CompanyPolicy, policy => policy.RequireAuthenticatedUser());

        services.AddScoped<CompanyContextResolver>();

        return services;
    }
}
