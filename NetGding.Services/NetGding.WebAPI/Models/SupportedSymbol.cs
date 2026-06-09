using System.Collections.Generic;

namespace NetGding.WebApi.Models;

public sealed record SupportedSymbol(
    string Symbol,
    bool IsEnabled,
    string? Name = null,
    IReadOnlyList<string>? Exchanges = null);
