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
            var normalized = Normalize(result.Symbol);
            var alternate = normalized.Contains('/')
                ? normalized.Replace('/', '_')
                : normalized.Replace('_', '/');

            var json = JsonSerializer.Serialize(result, s_jsonOptions);
            db.HashSet(RedisHashKey, [
                new HashEntry(normalized, json),
                new HashEntry(alternate, json)
            ]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisAnalysisCache] Failed to store analysis result for {Symbol}", result.Symbol);
        }
    }

    public AnalysisResult? GetLatest(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        try
        {
            var db = _redis.GetDatabase();
            var normalized = Normalize(symbol);
            var val = db.HashGet(RedisHashKey, normalized);

            if (val.IsNullOrEmpty)
            {
                var alternate = normalized.Contains('/')
                    ? normalized.Replace('/', '_')
                    : normalized.Replace('_', '/');
                val = db.HashGet(RedisHashKey, alternate);
            }

            if (val.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<AnalysisResult>(val.ToString(), s_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisAnalysisCache] Failed to retrieve analysis result for {Symbol}", symbol);
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
