using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class SymbolMetadataProvider : ISymbolMetadataProvider
{
    private readonly IOptionsMonitor<WebApiOptions> _options;

    public SymbolMetadataProvider(IOptionsMonitor<WebApiOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<SupportedSymbol> GetSupportedSymbols()
    {
        var symbols = _options.CurrentValue.Symbols ?? [];
        return symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new SupportedSymbol(x, true))
            .ToArray();
    }

    public IReadOnlyList<string> GetSupportedTimeframes()
    {
        var timeframes = _options.CurrentValue.BarTimeFrames ?? [];
        return timeframes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }
}