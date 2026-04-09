using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetGding.Analyzer.FinBert;

public sealed class FinBertSentimentAnalyzer : IFinBertSentimentAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly FinBertOptions _options;
    private readonly ILogger<FinBertSentimentAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, SentimentPrediction> _cache = new();

    public FinBertSentimentAnalyzer(
        HttpClient httpClient,
        IOptions<FinBertOptions> options,
        ILogger<FinBertSentimentAnalyzer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SentimentPrediction> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var hash = ComputeHash(text);

        if (_cache.TryGetValue(hash, out var cached))
        {
            _logger.LogDebug("Cache hit for text hash {Hash}", hash);
            return cached;
        }

        try
        {
            var prediction = await CallApiAsync(text, cancellationToken)
                .ConfigureAwait(false);

            _cache.TryAdd(hash, prediction);
            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinBERT API call failed for text: {Text}",
                text.Length > 80 ? text[..80] : text);

            return new SentimentPrediction(text, SentimentLabel.Neutral, 0f);
        }
    }

    private async Task<SentimentPrediction> CallApiAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var payload = new { inputs = text };

        var response = await _httpClient.PostAsJsonAsync(_options.InferenceUrl, payload, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var labelArray = root[0];

            if (labelArray.ValueKind == JsonValueKind.Array)
                return ParsePrediction(text, labelArray);

            return ParsePrediction(text, root);
        }

        return new SentimentPrediction(text, SentimentLabel.Neutral, 0f);
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