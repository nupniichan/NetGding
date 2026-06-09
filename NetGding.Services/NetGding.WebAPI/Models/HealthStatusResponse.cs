using System.Collections.Generic;

namespace NetGding.WebApi.Models;

public sealed record HealthStatusResponse(
    string Status,
    IReadOnlyList<ServiceHealthStatus> Services);
