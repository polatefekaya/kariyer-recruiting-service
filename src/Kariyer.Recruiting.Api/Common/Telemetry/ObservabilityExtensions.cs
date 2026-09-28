using Kariyer.Recruiting.Api.Common.Persistence;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Kariyer.Recruiting.Api.Common.Telemetry;

public static class ObservabilityExtensions
{
    public const string ServiceName = "kariyer-recruiting-service";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddMetrics();
        builder.Services.AddSingleton<RecruitingMetrics>();

        string? otlp = builder.Configuration["OpenTelemetry:Endpoint"];

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o => o.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(RecruitingMetrics.MeterName)
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
            });

        return builder;
    }

    public static IServiceCollection AddRecruitingHealthChecks(
        this IServiceCollection services, string postgres) =>
        services.AddHealthChecks()
            .AddNpgSql(postgres, name: "postgres", tags: ["ready"])
            .Services;
}
