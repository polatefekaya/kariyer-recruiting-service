using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Messaging;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

if (HealthCheckCommand.ShouldRun(args))
{
    return await HealthCheckCommand.RunAsync();
}

int exitCode = 0;

try
{
    WebApplication app = Build(args);

    await app.MigrateAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"FATAL: the recruiting service could not start. {ex}");
    Log.Fatal(ex, "The recruiting service could not start.");
    exitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

if (exitCode != 0)
{
    await Console.Error.FlushAsync();
    Environment.Exit(exitCode);
}

return exitCode;

static WebApplication Build(string[] args)
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddObservability();

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddRecruitingOptions(builder.Configuration, builder.Environment);

    string postgres = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

    builder.Services.AddRecruitingPersistence(postgres);
    builder.Services.AddGarnetCache(builder.Configuration);
    builder.Services.AddScoped<CacheInvalidator>();
    builder.Services.AddSingleton<InterviewConfirmationTokens>();
    builder.Services.AddRecruitingMessaging(builder.Configuration, builder.Configuration.GetConnectionString("RabbitMQ"));
    builder.Services.AddRecruitingAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddRecruitingHealthChecks(postgres);
    builder.Services.AddPortalCors(builder.Configuration);
    builder.Services.AddRecruitingRateLimiter(builder.Configuration);
    builder.Services.AddEndpoints();
    builder.Services.AddFeatureHandlers();
    builder.Services.AddProblemDetails();

    WebApplication app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors(CorsExtensions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication: the write limiter partitions by the token's subject, because behind
    // the gateway every request shares one remote IP.
    app.UseRateLimiter();

    app.MapEndpoints("/api/recruiting");
    app.MapPrometheusScrapingEndpoint();

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

    return app;
}
