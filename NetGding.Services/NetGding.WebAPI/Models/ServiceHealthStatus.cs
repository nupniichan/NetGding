namespace NetGding.WebApi.Models;

public sealed record ServiceHealthStatus(
    string Name,
    string Status,
    string? Message = null);
