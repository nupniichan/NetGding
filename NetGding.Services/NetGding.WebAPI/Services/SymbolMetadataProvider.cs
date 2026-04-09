using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class SymbolMetadataProvider : ISymbolMetadataProvider
{
    private readonly IOptionsMonitor<CollectorOptions> _collectorOptions;

    public SymbolMetadataProvider(IOptionsMonitor<CollectorOptions> collectorOptions)
    {
        _collectorOptions = collectorOptions;
    }

    public IReadOnlyList<SupportedSymbol> GetSupportedSymbols()
    {
        var symbols = _collectorOptions.CurrentValue.Symbols ?? [];
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
        var timeframes = _collectorOptions.CurrentValue.BarTimeFrames ?? [];
        return timeframes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }
}
