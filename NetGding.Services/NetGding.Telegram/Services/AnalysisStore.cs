using System.Collections.Concurrent;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Telegram.Services;

public sealed class AnalysisStore : IAnalysisStore
{
    private readonly ConcurrentDictionary<string, AnalysisResult> _store =
        new(StringComparer.OrdinalIgnoreCase);

    public void Store(AnalysisResult result)
    {
        var normalized = Normalize(result.Symbol);
        var alternate = normalized.Contains('/')
            ? normalized.Replace('/', '_')
            : normalized.Replace('_', '/');

        _store[normalized] = result;
        _store[alternate] = result;
    }

    public AnalysisResult? GetLatest(string symbol)
    {
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