using System.Collections.Concurrent;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Contracts.Services;

public sealed class InMemoryAnalysisCache : IAnalysisCache
{
    private readonly ConcurrentDictionary<string, AnalysisResult> _store =
        new(StringComparer.OrdinalIgnoreCase);

    public void Store(AnalysisResult result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Symbol))
            return;

        var normalizedSymbol = Normalize(result.Symbol);
        var alternateSymbol = normalizedSymbol.Contains('/')
            ? normalizedSymbol.Replace('/', '_')
            : normalizedSymbol.Replace('_', '/');

        _store[normalizedSymbol] = result;
        _store[alternateSymbol] = result;

        if (!string.IsNullOrWhiteSpace(result.Timeframe))
        {
            var tf = Normalize(result.Timeframe);
            _store[$"{normalizedSymbol}|{tf}"] = result;
            _store[$"{alternateSymbol}|{tf}"] = result;
        }
    }

    public AnalysisResult? GetLatest(string symbol, string? timeframe = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var normalizedSymbol = Normalize(symbol);
        var alternateSymbol = normalizedSymbol.Contains('/')
            ? normalizedSymbol.Replace('/', '_')
            : normalizedSymbol.Replace('_', '/');

        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            var tf = Normalize(timeframe);
            if (_store.TryGetValue($"{normalizedSymbol}|{tf}", out var tfResult))
                return tfResult;
            if (_store.TryGetValue($"{alternateSymbol}|{tf}", out var tfAltResult))
                return tfAltResult;
        }

        if (_store.TryGetValue(normalizedSymbol, out var result))
            return result;

        return _store.TryGetValue(alternateSymbol, out result) ? result : null;
    }

    public IReadOnlyDictionary<string, AnalysisResult> GetAll() => _store;

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();
}
