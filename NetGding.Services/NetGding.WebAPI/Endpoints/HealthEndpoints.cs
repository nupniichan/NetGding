using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", HandleHealthAsync)
           .WithName("GetHealth")
           .WithTags("Health");
    }

    private static async Task<IResult> HandleHealthAsync(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<WebApiOptions> options,
        CancellationToken ct)
    {
        var o = options.CurrentValue;
        var services = new List<ServiceHealthStatus>(3)
        {
            await ProbeAsync("collector", o.CollectorServiceUrl, o.HealthProbePath, httpClientFactory, ct)
                .ConfigureAwait(false),
            await ProbeAsync("telegram", o.TelegramServiceUrl, o.HealthProbePath, httpClientFactory, ct)
                .ConfigureAwait(false)
        };

        if (!string.IsNullOrWhiteSpace(o.AnalyzerServiceUrl))
        {
            services.Add(await ProbeAsync(
                "analyzer",
                o.AnalyzerServiceUrl,
                o.HealthProbePath,
                httpClientFactory,
                ct).ConfigureAwait(false));
        }

        var upCount = services.Count(x => x.Status.Equals("UP", StringComparison.OrdinalIgnoreCase));
        var overall = upCount == services.Count ? "UP" : upCount == 0 ? "DOWN" : "DEGRADED";

        return Results.Ok(new HealthStatusResponse(overall, services));
    }

    private static async Task<ServiceHealthStatus> ProbeAsync(
        string name,
        string? baseUrl,
        string probePath,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ServiceHealthStatus(name, "DOWN", "Service URL is not configured.");

        var normalizedPath = string.IsNullOrWhiteSpace(probePath) ? "/" : probePath;
        if (!normalizedPath.StartsWith('/'))
            normalizedPath = $"/{normalizedPath}";

        try
        {
            var http = httpClientFactory.CreateClient("HealthProbe");
            var target = $"{baseUrl.TrimEnd('/')}{normalizedPath}";
            using var response = await http.GetAsync(target, ct).ConfigureAwait(false);

            return new ServiceHealthStatus(
                name,
                "UP",
                $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new ServiceHealthStatus(name, "DOWN", ex.Message);
        }
    }
}
