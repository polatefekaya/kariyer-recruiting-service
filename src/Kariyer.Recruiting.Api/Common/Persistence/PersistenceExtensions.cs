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
        using IServiceScope scope = app.Services.CreateScope();
        RecruitingDbContext db = scope.ServiceProvider.GetRequiredService<RecruitingDbContext>();

        if (app.Services.GetRequiredService<IOptions<PersistenceOptions>>().Value.MigrateOnStartup)
        {
            await db.Database.MigrateAsync();
        }

        await EnsureLegacyStageFunctionAsync(db);
    }

    private static async Task EnsureLegacyStageFunctionAsync(RecruitingDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value text)
            RETURNS text
            LANGUAGE sql
            IMMUTABLE
            PARALLEL SAFE
            AS $$
                SELECT CASE lower(coalesce(value, ''))
                    WHEN 'pending'      THEN 'NEW'
                    WHEN 'under_review' THEN 'REVIEWING'
                    WHEN 'accepted'     THEN 'HIRED'
                    WHEN 'rejected'     THEN 'REJECTED'
                    WHEN 'withdrawn'    THEN 'WITHDRAWN'
                    ELSE 'NEW'
                END;
            $$;

            CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value anyelement)
            RETURNS text
            LANGUAGE sql
            IMMUTABLE
            PARALLEL SAFE
            AS $$
                SELECT recruiting.kz_stage_from_legacy(value::text);
            $$;

            DO $$
            DECLARE
                v_schema text;
            BEGIN
                SELECT n.nspname INTO v_schema
                FROM pg_type t
                JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE t.typname = 'enum_job_application_application_status'
                LIMIT 1;

                IF v_schema IS NOT NULL THEN
                    EXECUTE format('
                        CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value %I.enum_job_application_application_status)
                        RETURNS text
                        LANGUAGE sql
                        IMMUTABLE
                        PARALLEL SAFE
                        AS $func$
                            SELECT recruiting.kz_stage_from_legacy(value::text);
                        $func$;
                    ', v_schema);
                END IF;
            END $$;
            """);
    }
}
