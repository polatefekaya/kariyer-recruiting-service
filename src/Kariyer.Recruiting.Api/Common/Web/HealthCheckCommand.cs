namespace Kariyer.Recruiting.Api.Common.Web;

/// <summary>Container HEALTHCHECK re-enters this binary instead of shelling out to curl.</summary>
public static class HealthCheckCommand
{
    public static bool ShouldRun(string[] args) => args.Contains("--healthcheck", StringComparer.Ordinal);

    public static async Task<int> RunAsync()
    {
        string url = Environment.GetEnvironmentVariable("HEALTHCHECK_URL") ?? "http://localhost:8080/health/live";

        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}
