using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NetGding.Configurations.Bootstrap;

/// <summary>
/// Provides a minimal /health endpoint for Docker healthchecks and service discovery.
/// </summary>
public static class HealthEndpointExtensions
{
    public static IEndpointRouteBuilder MapMinimalHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "UP", timestamp = DateTime.UtcNow }))
           .WithName("HealthCheck")
           .WithTags("Health");

        return app;
    }
}
