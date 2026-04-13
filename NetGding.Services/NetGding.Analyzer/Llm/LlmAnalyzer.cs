using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Llm;

public sealed class LlmAnalyzer : ILlmAnalyzer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<LlmAnalyzer> _logger;

    public LlmAnalyzer(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<LlmAnalyzer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmSignal> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(request);

        var raw = await CallChatCompletionAsync(prompt, cancellationToken)
            .ConfigureAwait(false);

        return ParseResponse(raw, request);
    }

    private string BuildPrompt(AnalysisRequest req)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a professional financial market analyst providing SIGNAL ANALYSIS ONLY.");
        sb.AppendLine("Your role is to assess market conditions and return structured signals.");
        sb.AppendLine();
        sb.AppendLine("STRICT RULES:");
        sb.AppendLine("  - DO NOT make trading decisions (buy/sell/wait).");
        sb.AppendLine("  - DO NOT generate entry prices, stop-loss, or take-profit levels.");
        sb.AppendLine("  - Only analyze market conditions and assign confidence to your assessment.");
        sb.AppendLine("  - Respond ONLY with a valid JSON object. No markdown, no text outside JSON.");
        sb.AppendLine();

        sb.AppendLine($"Symbol: {req.Symbol}");
        sb.AppendLine($"Market: {req.Market}");
        sb.AppendLine($"Type: {req.MarketType}");
        sb.AppendLine($"Timeframe: {req.Timeframe}");
        sb.AppendLine($"Market Regime (pre-computed): {req.Regime}");
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

        sb.AppendLine("PRE-COMPUTED Indicators (use EXACT values below for your analysis — do NOT recalculate):");
        sb.AppendLine();
        sb.AppendLine("ANALYSIS PRIORITY (evaluate in this order):");
        sb.AppendLine("  1. Trend — EMA alignment (fast vs slow EMA cross)");
        sb.AppendLine("  2. Momentum — RSI level, MACD histogram direction");
        sb.AppendLine("  3. Volatility — ATR magnitude relative to price, Bollinger Band width");
        sb.AppendLine("  4. News — only use as a secondary modifier to confidence, not a primary driver");
        sb.AppendLine();
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
            sb.AppendLine($"Recent News ({req.News.Count} articles — use only as a modifier to confidence):");
            var count = Math.Min(req.News.Count, 10);
            for (int i = 0; i < count; i++)
            {
                var n = req.News[i];
                sb.AppendLine($"  - [{n.CreatedAtUtc:yyyy-MM-dd HH:mm}] {n.Headline}");
                if (!string.IsNullOrWhiteSpace(n.Summary))
                    sb.AppendLine($"    {n.Summary[..Math.Min(n.Summary.Length, 200)]}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Respond with ONLY a JSON object matching this exact schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"trend\": \"bullish|bearish|neutral\",");
        sb.AppendLine("  \"momentum\": \"strong|weak|divergence\",");
        sb.AppendLine("  \"volatility\": \"high|low\",");
        sb.AppendLine("  \"confidence\": 0.0,");
        sb.AppendLine("  \"reason\": \"concise explanation combining technical indicator analysis\",");
        sb.AppendLine("  \"newsImpact\": 0.0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Field rules:");
        sb.AppendLine("  - trend: bullish (price likely rising), bearish (price likely falling), neutral (no clear direction)");
        sb.AppendLine("  - momentum: strong (RSI>55 or <45, MACD histogram expanding), weak (RSI near 50, MACD flat), divergence (price/RSI diverging)");
        sb.AppendLine("  - volatility: high (ATR% > 2% of price or BB wide), low (ATR% < 1% of price or BB narrow)");
        sb.AppendLine("  - confidence: 0.0-1.0 reflecting how clearly indicators align with the stated trend");
        sb.AppendLine("  - reason: 1-2 sentences summarizing the indicator evidence");
        sb.AppendLine("  - newsImpact: -1.0 (strongly negative) to 1.0 (strongly positive), 0.0 if no news");

        return sb.ToString();
    }

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
                new { role = "system", content = "You are a professional financial market analyst providing signal analysis only. Always respond with valid JSON only. Never include trading decisions, entry prices, stop-loss, or take-profit in your response." },
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
                        "LLM: rate limited (429), waiting {Delay:g} before retry (attempt {Attempt}/{Max})",
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

        throw new HttpRequestException("LLM: max retry attempts exceeded.");
    }

    private LlmSignal ParseResponse(string raw, AnalysisRequest request)
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
            var signal = JsonSerializer.Deserialize<LlmSignal>(trimmed, s_jsonOptions);
            if (signal is not null)
                return signal;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM: failed to parse signal JSON, attempting extraction");
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var extracted = trimmed[start..(end + 1)];
            var signal = JsonSerializer.Deserialize<LlmSignal>(extracted, s_jsonOptions);
            if (signal is not null)
                return signal;
        }

        throw new InvalidOperationException(
            $"Could not parse LLM signal response for {request.Symbol} ({request.Timeframe}).");
    }
}