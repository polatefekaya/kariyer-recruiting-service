namespace Kariyer.Recruiting.Api.Common.Configuration;

public static class OptionsExtensions
{
    public static IServiceCollection AddRecruitingOptions(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<RecruitingOptions>()
            .Bind(configuration.GetSection(RecruitingOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => environment.IsDevelopment()
                    || (!string.IsNullOrWhiteSpace(options.ConfirmationSigningKey)
                        && options.ConfirmationSigningKey != RecruitingOptions.DevelopmentSigningKey),
                "Recruiting:ConfirmationSigningKey must be set outside Development; the candidate's "
                + "accept/decline links are signed with it.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GarnetOptions>()
            .Bind(configuration.GetSection(GarnetOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
