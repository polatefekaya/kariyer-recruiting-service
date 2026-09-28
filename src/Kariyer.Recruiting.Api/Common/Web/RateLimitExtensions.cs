using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Kariyer.Recruiting.Api.Common.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Web;

public static class RateLimitPolicies
{
    /// <summary>Everything that writes: stage moves, notes, invitations, reschedules, cancellations.</summary>
    public const string Write = "recruiting:write";

    /// <summary>The candidate's anonymous accept/decline routes.</summary>
    public const string Confirmation = "recruiting:confirmation";
}

public static class RateLimitExtensions
{
    /// <summary>
    /// Rate limits the mutating routes (technical document §11).
    ///
    /// This service runs behind the identity gateway with host networking, so every request
    /// arrives from 127.0.0.1: partitioning by remote IP would put the whole estate in one bucket
    /// and the first busy company would throttle everyone else. The write limiter therefore
    /// partitions by the authenticated subject, which the gateway cannot flatten — it forwards
    /// the bearer token and this service validates it itself.
    ///
    /// The candidate's confirmation routes have no subject at all. Their partition is the
    /// interview uid from the route, because the thing worth rate limiting there is guessing one
    /// interview's HMAC token; an attacker changing IP does not get a fresh budget.
    /// </summary>
    public static IServiceCollection AddRecruitingRateLimiter(
        this IServiceCollection services, IConfiguration configuration)
    {
        RateLimitOptions limits = new();
        configuration.GetSection(RateLimitOptions.SectionName).Bind(limits);

        if (!limits.Enabled)
        {
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Write, http => Fixed(Subject(http), limits.WritesPerMinute));

            options.AddPolicy(
                RateLimitPolicies.Confirmation,
                http => Fixed($"interview:{http.Request.RouteValues["interviewUid"]}", limits.ConfirmationsPerMinute));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(
                        ApiError.Create(ErrorCodes.TooManyRequests, "Çok fazla istek gönderildi. Biraz sonra tekrar deneyin."),
                        SerializerOptions),
                    cancellationToken);
            };
        });

        return services;
    }

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static RateLimitPartition<string> Fixed(string key, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            // No queue: a recruiter who hits the ceiling should be told, not held on a socket.
            QueueLimit = 0,
        });

    /// The token's subject, not the connection — see the note above about the gateway.
    private static string Subject(HttpContext http) =>
        http.User.FindFirstValue("sub")
        ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? $"ip:{http.Connection.RemoteIpAddress}";
}
