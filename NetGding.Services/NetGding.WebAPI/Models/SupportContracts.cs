namespace NetGding.WebApi.Models;

public sealed record SupportedSymbol(
    string Symbol,
    bool IsEnabled,
    string? Name = null,
    IReadOnlyList<string>? Exchanges = null);

public sealed record SupportConfigResponse(
    IReadOnlyList<SupportedSymbol> Symbols,
    IReadOnlyList<string> Timeframes,
    string Environment,
    string Version);
