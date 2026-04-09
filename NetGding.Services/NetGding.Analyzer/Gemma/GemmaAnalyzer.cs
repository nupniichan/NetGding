using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Gemma;

public sealed class GemmaAnalyzer : IGemmaAnalyzer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly GemmaOptions _options;
    private readonly ILogger<GemmaAnalyzer> _logger;

    public GemmaAnalyzer(
        HttpClient httpClient,
        IOptions<GemmaOptions> options,
        ILogger<GemmaAnalyzer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(request);

        try
        {
            var raw = await CallChatCompletionAsync(prompt, cancellationToken)
                .ConfigureAwait(false);

            var result = ParseResponse(raw, request);
            result.AnalyzedAtUtc = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemma analysis failed for {Symbol} ({Timeframe})",
                request.Symbol, request.Timeframe);

            return FallbackResult(request);
        }
    }

    private string BuildPrompt(AnalysisRequest req)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a professional financial market analyst. Analyze the following market data and return your analysis ONLY as a valid JSON object (no markdown, no explanation outside JSON).");
        sb.AppendLine();

        sb.AppendLine($"Symbol: {req.Symbol}");
        sb.AppendLine($"Market: {req.Market}");
        sb.AppendLine($"Type: {req.MarketType}");
        sb.AppendLine($"Timeframe: {req.Timeframe}");
        sb.AppendLine();

        var bars = req.Bars;
        if (bars.Count > 0)
        {
            var last = bars[^1];
            sb.AppendLine($"Current Price: {last.Close}");
            sb.AppendLine();

            sb.AppendLine("Recent OHLCV (last 20 bars):");
            var start = Math.Max(0, bars.Count - 20);
            for (int i = start; i < bars.Count; i++)
            {
                var b = bars[i];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0:yyyy-MM-dd HH:mm} O={1} H={2} L={3} C={4} V={5}",
                    b.TimestampUtc, b.Open, b.High, b.Low, b.Close, b.Volume));
            }
            sb.AppendLine();
        }

        sb.AppendLine("PRE-COMPUTED Indicators (calculated from full historical dataset — copy these EXACT values into the indicators field, do NOT recalculate from OHLCV bars above):");
        AppendIndicatorDict(sb, "EMA", req.Indicators.Ema);
        AppendIndicatorDict(sb, "MACD", req.Indicators.Macd);
        AppendIndicatorDict(sb, "RSI", req.Indicators.Rsi);
        AppendIndicatorDict(sb, "BollingerBands", req.Indicators.BollingerBands);
        AppendIndicatorDict(sb, "ATR", req.Indicators.Atr);
        AppendIndicatorDict(sb, "VolumeMa", req.Indicators.VolumeMa);
        AppendIndicatorDict(sb, "VWAP", req.Indicators.Vwap);
        sb.AppendLine();

        if (req.News.Count > 0)
        {
            sb.AppendLine($"Recent News ({req.News.Count} articles):");
            var count = Math.Min(req.News.Count, 10);
            for (int i = 0; i < count; i++)
            {
                var n = req.News[i];
                sb.AppendLine($"  - [{n.CreatedAtUtc:yyyy-MM-dd HH:mm}] {n.Headline}");
                if (!string.IsNullOrWhiteSpace(n.Summary))
                    sb.AppendLine($"    {n.Summary[..Math.Min(n.Summary.Length, 200)]}");
            }
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: Analyze the news articles above to determine their sentiment impact on the asset price.");
            sb.AppendLine("Consider how positive or negative news may affect short-term and mid-term price movement.");
            sb.AppendLine("Factor the news sentiment into your trade decision, reason, and risk levels.");
            sb.AppendLine();
        }

        sb.AppendLine("Respond with ONLY a JSON object matching this exact schema (the indicator values shown are the pre-computed values you MUST use):");
        sb.AppendLine("{");
        sb.AppendLine($"  \"symbol\": \"{req.Symbol}\",");
        sb.AppendLine("  \"market\": \"stock|crypto|forex\",");
        sb.AppendLine("  \"marketType\": \"future|spot\",");
        sb.AppendLine($"  \"timeframe\": \"{req.Timeframe}\",");
        sb.AppendLine("  \"currentPrice\": 0.0,");
        sb.Append("  \"indicators\": ");
        sb.AppendLine(BuildIndicatorSchema(req.Indicators) + ",");
        sb.AppendLine("  \"marketStructure\": {");
        sb.AppendLine("    \"shortTermTrend\": \"uptrend|downtrend|sideways\",");
        sb.AppendLine("    \"midTermTrend\": \"uptrend|downtrend|sideways\",");
        sb.AppendLine("    \"longTermTrend\": \"uptrend|downtrend|sideways\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"decision\": \"buy|sell|wait\",");
        sb.AppendLine("  \"reason\": \"detailed reason combining technical and news analysis\",");
        sb.AppendLine($"  \"expectedHoldTime\": \"{ResolveHoldTimeHint(req.Timeframe)}\",");
        sb.AppendLine("  \"riskManagement\": {");
        sb.AppendLine("    \"futures\": { \"entry\": 0.0, \"stopLoss\": 0.0, \"takeProfit\": 0.0 },");
        sb.AppendLine("    \"spot\": { \"buyPrice\": 0.0, \"dcaLevels\": [0.0, 0.0] }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"newsSentiment\": \"positive|negative|neutral|none\",");
        sb.AppendLine("  \"newsSummary\": \"brief summary of how news impacts the trading decision\",");
        sb.AppendLine("  \"analyzedAtUtc\": \"2025-01-01T00:00:00Z\"");
        sb.AppendLine("}");
        sb.AppendLine("Provide actionable entry/SL/TP for the given market type.");
        sb.AppendLine("If news articles are provided, analyze their sentiment and explain their impact on the decision. Set newsSentiment to 'none' if no news is available.");

        return sb.ToString();
    }

    private static string BuildIndicatorSchema(IndicatorSnapshot indicators)
    {
        var sections = new List<string>();
        AppendIndicatorSchemaSection(sections, "ema", indicators.Ema);
        AppendIndicatorSchemaSection(sections, "macd", indicators.Macd);
        AppendIndicatorSchemaSection(sections, "rsi", indicators.Rsi);
        AppendIndicatorSchemaSection(sections, "bollingerBands", indicators.BollingerBands);
        AppendIndicatorSchemaSection(sections, "atr", indicators.Atr);
        AppendIndicatorSchemaSection(sections, "volumeMa", indicators.VolumeMa);
        AppendIndicatorSchemaSection(sections, "vwap", indicators.Vwap);

        return "{\n    " + string.Join(",\n    ", sections) + "\n  }";
    }

    private static void AppendIndicatorSchemaSection(List<string> sections, string key, Dictionary<string, float> dict)
    {
        if (dict.Count == 0) return;
        var entries = string.Join(", ", dict.Select(kv =>
            $"\"{kv.Key}\": {kv.Value.ToString(CultureInfo.InvariantCulture)}"));
        sections.Add($"\"{key}\": {{{entries}}}");
    }

    private static string ResolveHoldTimeHint(string timeframe) =>
        timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" => "1-4 hours",
            "1h" or "1hour" or "60m" => "4-12 hours",
            "4h" or "4hour" or "240m" => "1-3 days",
            "1d" or "1day" or "d" => "3-14 days",
            "1w" or "1week" or "w" => "2-8 weeks",
            "1m" or "1month" or "mo" => "1-6 months",
            _ => "depends on timeframe"
        };

    private static void AppendIndicatorDict(StringBuilder sb, string name, Dictionary<string, float> dict)
    {
        if (dict.Count == 0) return;
        var values = string.Join(", ", dict.Select(kv =>
            $"{kv.Key}={kv.Value.ToString(CultureInfo.InvariantCulture)}"));
        sb.AppendLine($"  {name}: {values}");
    }

    private async Task<string> CallChatCompletionAsync(string prompt, CancellationToken ct)
    {
        var maxAttempts = _options.MaxAttempts;
        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";

        var payload = new
        {
            model = _options.ModelName,
            messages = new[]
            {
                new { role = "system", content = "You are a professional financial market analyst. Always respond with valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens
        };
        var payloadJson = JsonSerializer.Serialize(payload);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxAttempts)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(Math.Pow(2, attempt) * 10);

                    _logger.LogWarning(
                        "Gemma: rate limited (429), waiting {Delay:g} before retry (attempt {Attempt}/{Max})",
                        retryAfter, attempt, maxAttempts);

                    await Task.Delay(retryAfter, ct).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
            }
        }
        finally
        {
            _gate.Release();
        }

        throw new HttpRequestException("Gemma: max retry attempts exceeded.");
    }

    private AnalysisResult ParseResponse(string raw, AnalysisRequest request)
    {
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("```"))
        {
            var first = trimmed.IndexOf('\n');
            var last = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (first >= 0 && last > first)
                trimmed = trimmed[(first + 1)..last].Trim();
        }

        try
        {
            var result = JsonSerializer.Deserialize<AnalysisResult>(trimmed, s_jsonOptions);
            if (result is not null)
            {
                result.Symbol = request.Symbol;
                result.Timeframe = request.Timeframe;
                return result;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemma JSON response, attempting extraction");
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var extracted = trimmed[start..(end + 1)];
            var result = JsonSerializer.Deserialize<AnalysisResult>(extracted, s_jsonOptions);
            if (result is not null)
            {
                result.Symbol = request.Symbol;
                result.Timeframe = request.Timeframe;
                return result;
            }
        }

        _logger.LogWarning("Could not parse Gemma response, returning fallback");
        return FallbackResult(request);
    }

    private static AnalysisResult FallbackResult(AnalysisRequest request)
    {
        var price = request.Bars.Count > 0 ? (decimal)request.Bars[^1].Close : 0m;
        return new AnalysisResult
        {
            Symbol = request.Symbol,
            Market = request.Market,
            MarketType = request.MarketType,
            Timeframe = request.Timeframe,
            CurrentPrice = price,
            Indicators = request.Indicators,
            Decision = TradeDecision.Wait,
            Reason = "Analysis unavailable — model response could not be parsed.",
            NewsSentiment = "none",
            NewsSummary = "",
            AnalyzedAtUtc = DateTime.UtcNow
        };
    }
}