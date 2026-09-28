using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddRecruitingPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RecruitingDbContext>((sp, options) =>
        {
            PersistenceOptions persistence = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RecruitingDbContext.Schema);
                npgsql.CommandTimeout(persistence.CommandTimeoutSeconds);
                npgsql.EnableRetryOnFailure(3);
            });

            // Reads dominate this service and every write goes through an explicitly tracked
            // aggregate, so tracking is opt-in rather than the default.
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<IApplicationPipelineRepository, ApplicationPipelineRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<IApplicationNoteRepository, ApplicationNoteRepository>();
        services.AddScoped<IActivityWriter, ActivityWriter>();
        services.AddScoped<ISavedFilterRepository, SavedFilterRepository>();
        services.AddScoped<IApplicationReadStore, ApplicationReadStore>();
        services.AddScoped<ICompanyDirectory, CompanyDirectory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static async Task MigrateAsync(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<PersistenceOptions>>().Value.MigrateOnStartup)
        {
            return;
        }

        using IServiceScope scope = app.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<RecruitingDbContext>().Database.MigrateAsync();
    }
}
