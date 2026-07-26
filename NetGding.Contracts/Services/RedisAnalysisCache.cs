using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Models.Analysis;
using StackExchange.Redis;

namespace NetGding.Contracts.Services;

public sealed class RedisAnalysisCache : IAnalysisCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAnalysisCache> _logger;
    private const string RedisHashKey = "cache:analysis:latest";

    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Options;

    public RedisAnalysisCache(
        IConnectionMultiplexer redis,
        ILogger<RedisAnalysisCache> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Store(AnalysisResult result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Symbol))
            return;

        try
        {
            var db = _redis.GetDatabase();
            var normalizedSymbol = Normalize(result.Symbol);
            var alternateSymbol = normalizedSymbol.Contains('/')
                ? normalizedSymbol.Replace('/', '_')
                : normalizedSymbol.Replace('_', '/');

            var json = JsonSerializer.Serialize(result, s_jsonOptions);
            var entries = new List<HashEntry>
            {
                new(normalizedSymbol, json),
                new(alternateSymbol, json)
            };

            if (!string.IsNullOrWhiteSpace(result.Timeframe))
            {
                var tf = Normalize(result.Timeframe);
                entries.Add(new($"{normalizedSymbol}|{tf}", json));
                entries.Add(new($"{alternateSymbol}|{tf}", json));
            }

            db.HashSet(RedisHashKey, entries.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisAnalysisCache] Failed to store analysis result for {Symbol} ({Timeframe})", result.Symbol, result.Timeframe);
        }
    }

    public AnalysisResult? GetLatest(string symbol, string? timeframe = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        try
        {
            var db = _redis.GetDatabase();
            var normalizedSymbol = Normalize(symbol);
            var alternateSymbol = normalizedSymbol.Contains('/')
                ? normalizedSymbol.Replace('/', '_')
                : normalizedSymbol.Replace('_', '/');

            RedisValue val = RedisValue.Null;

            if (!string.IsNullOrWhiteSpace(timeframe))
            {
                var tf = Normalize(timeframe);
                val = db.HashGet(RedisHashKey, $"{normalizedSymbol}|{tf}");
                if (val.IsNullOrEmpty)
                    val = db.HashGet(RedisHashKey, $"{alternateSymbol}|{tf}");
            }

            if (val.IsNullOrEmpty)
            {
                val = db.HashGet(RedisHashKey, normalizedSymbol);
                if (val.IsNullOrEmpty)
                    val = db.HashGet(RedisHashKey, alternateSymbol);
            }

            if (val.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<AnalysisResult>(val.ToString(), s_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisAnalysisCache] Failed to retrieve analysis result for {Symbol} ({Timeframe})", symbol, timeframe);
            return null;
        }
    }

    public IReadOnlyDictionary<string, AnalysisResult> GetAll()
    {
        var dict = new Dictionary<string, AnalysisResult>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var db = _redis.GetDatabase();
            var entries = db.HashGetAll(RedisHashKey);
            foreach (var entry in entries)
            {
                if (entry.Value.IsNullOrEmpty) continue;
                var res = JsonSerializer.Deserialize<AnalysisResult>(entry.Value.ToString(), s_jsonOptions);
                if (res is not null && !string.IsNullOrWhiteSpace(entry.Name))
                {
                    dict[entry.Name!] = res;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisAnalysisCache] Failed to retrieve all cached analysis results");
        }
        return dict;
    }

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();
}
