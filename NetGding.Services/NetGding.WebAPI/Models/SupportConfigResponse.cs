using System.Collections.Generic;

namespace NetGding.WebApi.Models;

public sealed record SupportConfigResponse(
    IReadOnlyList<SupportedSymbol> Symbols,
    IReadOnlyList<string> Timeframes,
    string Environment,
    string Version);
