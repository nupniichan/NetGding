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

        sb.AppendLine("You are a Senior Quant Strategist & Wall Street Proprietary Trader with 15+ years of institutional trading experience.");
        sb.AppendLine("Your task is to analyze the market context and generate the primary signal parameters.");
        sb.AppendLine();
        sb.AppendLine("STRICT RULES:");
        sb.AppendLine("  - DO NOT make direct trading decisions (buy/sell/wait). The executing system will evaluate the signal parameters.");
        sb.AppendLine("  - DO NOT generate specific trade entry, stop-loss, or take-profit levels. The risk calculator will compute them.");
        sb.AppendLine("  - Respond ONLY with a valid JSON object. No markdown formatting (no ```json code blocks), no text outside the JSON.");
        sb.AppendLine();
        sb.AppendLine("REGIME-AWARE STRATEGY INSTRUCTIONS:");
        sb.AppendLine($"  Current pre-computed Market Regime is: {req.Regime.ToString().ToUpperInvariant()}");
        sb.AppendLine("  - TRENDING Regime: Prioritize trend-following indicators. Strong trends are indicated by clear EMA stacking (Fast > Mid > Slow for Bullish, or Fast < Mid < Slow for Bearish) and MACD histogram expansion.");
        sb.AppendLine("  - RANGING Regime: Prioritize mean-reversion. Look for momentum exhaustion (Weak or Divergence) near Support/Resistance bands and Bollinger Band extremes. Ignore lagging EMA trend stackings; focus on bounce/rejection signals.");
        sb.AppendLine("  - VOLATILE Regime: Exercise high caution. Look for key structural breakouts (Bollinger Band expansions, extreme ATR relative to price). Prioritize conservative analysis and reduce confidence if indicators conflict.");
        sb.AppendLine();
        if (req.FearAndGreedIndex.HasValue)
        {
            sb.AppendLine($"  Global Crypto Sentiment (Fear & Greed Index): {req.FearAndGreedIndex.Value} ({req.FearAndGreedClassification})");
            sb.AppendLine();
        }
        sb.AppendLine("INDICATOR INTERPRETATION GUIDELINES (Use Confluence):");
        sb.AppendLine("  1. Trend Alignment: Verify EMA levels (e.g., 9, 21, 50, 100, 200). Stacking alignment indicates trend strength. Flat or tangled lines indicate sideways/ranging.");
        sb.AppendLine("  2. Momentum Validation: MACD histogram expansion/contraction and RSI levels. Identify any divergence between price action and momentum (e.g. price making new highs but RSI or MACD showing lower highs) which strongly signals trend exhaustion.");
        sb.AppendLine("  3. Volume & VWAP: Use Volume/VolumeMA and VWAP. Price above VWAP shows institutional buying dominance. Price below VWAP shows selling dominance. Moves supported by rising volume are more sustainable.");
        sb.AppendLine("  4. Support & Resistance: Check price proximity to S/R levels. Entering near a major S/R level offers a highly asymmetric risk-reward ratio.");
        sb.AppendLine("  5. News Sentiment: Factor in news headlines as a sentiment modifier only.");
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

            var tfNormalized = req.Timeframe.ToLowerInvariant();
            int recentBarsCount = tfNormalized switch
            {
                "15m" or "15min" => 40,
                "1h" or "1hour" => 30,
                _ => 20
            };

            sb.AppendLine($"Recent OHLCV (last {recentBarsCount} bars):");
            var start = Math.Max(0, bars.Count - recentBarsCount);
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
        AppendIndicatorDict(sb, "EMA", req.Indicators.Ema);
        AppendIndicatorDict(sb, "MACD", req.Indicators.Macd);
        AppendIndicatorDict(sb, "RSI", req.Indicators.Rsi);
        AppendIndicatorDict(sb, "BollingerBands", req.Indicators.BollingerBands);
        AppendIndicatorDict(sb, "ATR", req.Indicators.Atr);
        AppendIndicatorDict(sb, "VolumeMa", req.Indicators.VolumeMa);
        AppendIndicatorDict(sb, "VWAP", req.Indicators.Vwap);
        AppendIndicatorDict(sb, "SupportResistance", req.Indicators.SupportResistance);
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

        sb.AppendLine("Respond with ONLY a JSON object matching this exact schema (lowercase keys):");
        sb.AppendLine("{");
        sb.AppendLine("  \"trend\": \"bullish|bearish|neutral\",");
        sb.AppendLine("  \"momentum\": \"strong|weak|divergence\",");
        sb.AppendLine("  \"volatility\": \"high|low\",");
        sb.AppendLine("  \"confidence\": 0.0,");
        sb.AppendLine("  \"reason\": \"professional market commentary explaining the confluence of indicators\",");
        sb.AppendLine("  \"newsImpact\": 0.0");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Field rules:");
        sb.AppendLine("  - trend: Use 'bullish' for upward structure, 'bearish' for downward structure, or 'neutral' for consolidation/ranging.");
        sb.AppendLine("  - momentum: Use 'strong' for high directional momentum, 'weak' for flat/consolidating, or 'divergence' for exhaustion/reversal indications (RSI/MACD moving opposite to price).");
        sb.AppendLine("  - volatility: Use 'high' (ATR relative to price is high, or Bollinger Bands wide) or 'low' (ATR is low, or Bollinger Bands narrow/squeezing).");
        sb.AppendLine("  - confidence: Score 0.0-1.0 (representing 0% to 100% confidence). Calculate this score strictly based on indicator confluence:");
        sb.AppendLine("    * If all key indicators (EMA trends, MACD, RSI, Bollinger Bands, Support/Resistance proximity, VWAP) agree on the direction -> Score: 0.90 to 0.95.");
        sb.AppendLine("    * If key indicators are conflicting (e.g., RSI is deeply oversold but EMA stack remains strongly bearish, or price is ranging but indicators show mixed signals) -> Score: MUST be below 0.50. This indicates high risk or incomplete criteria, which will trigger the system to make a WAIT decision.");
        sb.AppendLine("  - reason: A highly professional 1-2 sentence commentary. Use sophisticated institutional terminology (e.g. 'liquidity sweep', 'momentum exhaustion near major resistance', 'bullish EMA structure supported by institutional volume above VWAP'). Do not say 'the trend is bullish' or reference JSON field values directly.");
        sb.AppendLine("  - newsImpact: -1.0 (strongly negative) to 1.0 (strongly positive), 0.0 if neutral or no news.");

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
                new { role = "system", content = "You are a Senior Quant Strategist & Wall Street Proprietary Trader. Provide institutional-grade, data-driven market signal analysis. You must output a valid JSON object ONLY. Never include markdown formatting or any text outside the JSON." },
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