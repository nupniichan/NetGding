namespace NetGding.WebApi.Models;

public sealed record ServiceHealthStatus(
    string Name,
    string Status,
    string? Message = null);

public sealed record HealthStatusResponse(
    string Status,
    IReadOnlyList<ServiceHealthStatus> Services);
