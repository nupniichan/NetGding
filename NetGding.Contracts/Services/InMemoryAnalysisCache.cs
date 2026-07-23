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

        var normalized = Normalize(result.Symbol);
        var alternate = normalized.Contains('/')
            ? normalized.Replace('/', '_')
            : normalized.Replace('_', '/');

        _store[normalized] = result;
        _store[alternate] = result;
    }

    public AnalysisResult? GetLatest(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var normalized = Normalize(symbol);
        if (_store.TryGetValue(normalized, out var result))
            return result;

        var alternate = normalized.Contains('/')
            ? normalized.Replace('/', '_')
            : normalized.Replace('_', '/');

        return _store.TryGetValue(alternate, out result) ? result : null;
    }

    public IReadOnlyDictionary<string, AnalysisResult> GetAll() => _store;

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();
}
