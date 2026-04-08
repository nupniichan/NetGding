using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NetGding.Analyzer.FinBert;

public sealed class FinBertSentimentAnalyzer : IFinBertSentimentAnalyzer
{
    private const string ApiUrl = "https://api-inference.huggingface.co/models/ProsusAI/finbert";
    private const int MaxBatchSize = 32;

    private readonly HttpClient _httpClient;
    private readonly ILogger<FinBertSentimentAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, SentimentPrediction> _cache = new();

    public FinBertSentimentAnalyzer(
        HttpClient httpClient,
        ILogger<FinBertSentimentAnalyzer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SentimentPrediction>> AnalyzeBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SentimentPrediction>(texts.Count);

        // Separate cached vs uncached
        var uncachedTexts = new List<(int Index, string Text, string Hash)>();
        for (int i = 0; i < texts.Count; i++)
        {
            var hash = ComputeHash(texts[i]);
            if (_cache.TryGetValue(hash, out var cached))
            {
                results.Add(cached);
            }
            else
            {
                uncachedTexts.Add((i, texts[i], hash));
                results.Add(null!);
            }
        }

        if (uncachedTexts.Count == 0)
        {
            _logger.LogDebug("All {Count} texts found in cache, skipping API call", texts.Count);
            return results;
        }

        _logger.LogDebug("Analyzing {UncachedCount}/{TotalCount} uncached texts via FinBERT",
            uncachedTexts.Count, texts.Count);

        // Process uncached texts in batches
        for (int batchStart = 0; batchStart < uncachedTexts.Count; batchStart += MaxBatchSize)
        {
            var batch = uncachedTexts
                .Skip(batchStart)
                .Take(MaxBatchSize)
                .ToList();

            var batchTexts = batch.Select(b => b.Text).ToList();

            try
            {
                var predictions = await CallApiAsync(batchTexts, cancellationToken)
                    .ConfigureAwait(false);

                for (int j = 0; j < predictions.Count && j < batch.Count; j++)
                {
                    var pred = predictions[j];
                    var entry = batch[j];

                    _cache.TryAdd(entry.Hash, pred);

                    results[FindResultIndex(texts, entry.Index)] = pred;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinBERT API call failed for batch starting at {Start}", batchStart);

                foreach (var entry in batch)
                {
                    var fallback = new SentimentPrediction(entry.Text, SentimentLabel.Neutral, 0f);
                    results[FindResultIndex(texts, entry.Index)] = fallback;
                }
            }
        }

        return results;
    }

    private static int FindResultIndex(IReadOnlyList<string> texts, int originalIndex) => originalIndex;

    private async Task<IReadOnlyList<SentimentPrediction>> CallApiAsync(
        List<string> texts,
        CancellationToken cancellationToken)
    {
        var payload = new { inputs = texts };

        var response = await _httpClient.PostAsJsonAsync(ApiUrl, payload, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<SentimentPrediction>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var first = root[0];
            if (first.ValueKind == JsonValueKind.Array)
            {
                for (int i = 0; i < root.GetArrayLength() && i < texts.Count; i++)
                {
                    var prediction = ParsePrediction(texts[i], root[i]);
                    results.Add(prediction);
                }
            }
            else
            {
                var prediction = ParsePrediction(texts[0], root);
                results.Add(prediction);
            }
        }

        return results;
    }

    private static SentimentPrediction ParsePrediction(string text, JsonElement labelArray)
    {
        var bestLabel = SentimentLabel.Neutral;
        float bestScore = 0f;

        foreach (var item in labelArray.EnumerateArray())
        {
            var label = item.GetProperty("label").GetString()?.ToLowerInvariant();
            var score = item.GetProperty("score").GetSingle();

            if (score > bestScore)
            {
                bestScore = score;
                bestLabel = label switch
                {
                    "positive" => SentimentLabel.Positive,
                    "negative" => SentimentLabel.Negative,
                    _ => SentimentLabel.Neutral
                };
            }
        }

        return new SentimentPrediction(text, bestLabel, bestScore);
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16];
    }
}