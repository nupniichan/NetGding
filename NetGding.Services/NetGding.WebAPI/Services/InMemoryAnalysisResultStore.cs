using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public sealed class InMemoryAnalysisResultStore : IAnalysisResultStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<AnalysisResult>> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsMonitor<WebApiOptions> _options;

    public InMemoryAnalysisResultStore(IOptionsMonitor<WebApiOptions> options)
    {
        _options = options;
    }

    public void Store(AnalysisResult result)
    {
        var key = BuildKey(result.Symbol, result.Timeframe);
        var queue = _store.GetOrAdd(key, static _ => new ConcurrentQueue<AnalysisResult>());
        queue.Enqueue(result);

        var maxItems = Math.Max(1, _options.CurrentValue.AnalysisHistoryLimit);
        while (queue.Count > maxItems)
            queue.TryDequeue(out _);
    }

    public AnalysisResult? GetLatest(string symbol, string timeframe)
    {
        var key = BuildKey(symbol, timeframe);
        return !_store.TryGetValue(key, out var queue)
            ? null
            : queue.OrderByDescending(x => x.AnalyzedAtUtc).FirstOrDefault();
    }

    public IReadOnlyList<AnalysisResult> GetHistory(
        string symbol,
        string timeframe,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        var key = BuildKey(symbol, timeframe);
        if (!_store.TryGetValue(key, out var queue))
            return [];

        var query = queue.AsEnumerable();
        if (fromUtc.HasValue)
            query = query.Where(x => x.AnalyzedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.AnalyzedAtUtc <= toUtc.Value);

        return query
            .OrderByDescending(x => x.AnalyzedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();
    }

    private static string BuildKey(string symbol, string timeframe) =>
        $"{Normalize(symbol)}|{Normalize(timeframe)}";

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
