using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Llm;

public sealed class LlmAnalyzer : ILlmAnalyzer
{
    private static readonly JsonSerializerOptions s_jsonOptions = NetGding.Contracts.JsonDefaults.Options;

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

        sb.AppendLine($"SYMBOL: {req.Symbol} | MARKET: {req.Market} | TYPE: {req.MarketType} | TF: {req.Timeframe}");
        sb.AppendLine($"REGIME: {req.Regime.ToString().ToUpperInvariant()}");
        sb.AppendLine();

        var regimeInstruction = req.Regime.ToString().ToUpperInvariant() switch
        {
            "TRENDING" => "Strategy: Trend-following. Confirm via EMA stacking (Fast>Mid>Slow=Bullish) and MACD expansion.",
            "RANGING" => "Strategy: Mean-reversion. Focus on momentum exhaustion near S/R and BB extremes. Ignore lagging EMA stacking.",
            "VOLATILE" => "Strategy: Conservative. Prioritize structural breakouts (BB expansion, extreme ATR). Reduce confidence if indicators conflict.",
            _ => "Strategy: Evaluate all indicators with equal weight."
        };
        sb.AppendLine(regimeInstruction);
        sb.AppendLine();

        if (req.FearAndGreedIndex.HasValue)
        {
            sb.AppendLine($"Sentiment (Fear & Greed): {req.FearAndGreedIndex.Value} ({req.FearAndGreedClassification})");
            sb.AppendLine();
        }

        sb.AppendLine("CONFLUENCE CHECKLIST:");
        sb.AppendLine("- EMA stacking -> trend strength (aligned=strong, tangled=weak)");
        sb.AppendLine("- MACD histogram + RSI -> momentum (divergence from price = exhaustion)");
        sb.AppendLine("- Price vs VWAP + Volume/VolumeMA -> institutional flow");
        sb.AppendLine("- S/R proximity -> risk-reward asymmetry");
        sb.AppendLine("- News -> sentiment modifier only");
        sb.AppendLine();

        var bars = req.Bars;
        if (bars.Count > 0)
        {
            var last = bars[^1];
            sb.AppendLine($"Current Price: {last.Close}");

            const int recentBarsCount = 15;
            sb.AppendLine($"Recent OHLCV (last {Math.Min(recentBarsCount, bars.Count)} bars):");
            var start = Math.Max(0, bars.Count - recentBarsCount);
            for (int i = start; i < bars.Count; i++)
            {
                var b = bars[i];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0:MM-dd HH:mm}\tO={1}\tH={2}\tL={3}\tC={4}\tV={5}",
                    b.TimestampUtc, b.Open, b.High, b.Low, b.Close, b.Volume));
            }
            sb.AppendLine();
        }

        sb.AppendLine("INDICATORS (pre-computed, use as-is):");
        AppendIndicatorDict(sb, "EMA", req.Indicators.Ema);
        AppendIndicatorDict(sb, "MACD", req.Indicators.Macd);
        AppendIndicatorDict(sb, "RSI", req.Indicators.Rsi);
        AppendIndicatorDict(sb, "BB", req.Indicators.BollingerBands);
        AppendIndicatorDict(sb, "ATR", req.Indicators.Atr);
        AppendIndicatorDict(sb, "VolMA", req.Indicators.VolumeMa);
        AppendIndicatorDict(sb, "VWAP", req.Indicators.Vwap);
        AppendIndicatorDict(sb, "S/R", req.Indicators.SupportResistance);
        sb.AppendLine();

        if (req.News.Count > 0)
        {
            var count = Math.Min(req.News.Count, 5);
            sb.AppendLine($"News ({count} articles, sentiment modifier only):");
            for (int i = 0; i < count; i++)
            {
                var n = req.News[i];
                sb.AppendLine($"  - [{n.CreatedAtUtc:MM-dd HH:mm}] {n.Headline}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Respond ONLY with a JSON object matching this exact schema:");
        sb.AppendLine("{");
        sb.AppendLine("  \"trend\": \"bullish|bearish|neutral\",");
        sb.AppendLine("  \"momentum\": \"strong|weak|divergence\",");
        sb.AppendLine("  \"volatility\": \"high|low\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"reason\": \"1-2 sentence institutional commentary\",");
        sb.AppendLine("  \"newsImpact\": -1.0 to 1.0");
        sb.AppendLine("}");
        sb.AppendLine("Field rules:");
        sb.AppendLine("- confidence: score 0.0-1.0 (>=0.90 if all indicators align; MUST be <0.50 if indicators conflict).");
        sb.AppendLine("- reason: institutional commentary (e.g. 'momentum exhaustion near major resistance'). Do NOT reference JSON field names directly.");
        sb.AppendLine("- newsImpact: -1.0 (strongly negative) to 1.0 (strongly positive), 0.0 if neutral/no news.");

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
        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";

        var payload = new
        {
            model = _options.ModelName,
            messages = new[]
            {
                new { role = "system", content = "You are a Senior Quant Strategist & Wall Street Proprietary Trader. Provide institutional-grade, data-driven market signal analysis. Output ONLY a valid JSON object without markdown formatting or text outside JSON." },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens
        };
        var payloadJson = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogError(
                "LLM: request failed with status code {StatusCode} ({Reason}) for model '{ModelName}': {Body}",
                (int)response.StatusCode, response.ReasonPhrase, _options.ModelName, errorBody);

            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.LlmRequestFailed,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) for model '{_options.ModelName}': {errorBody}");
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogError("LLM: API returned an empty response body for model '{ModelName}'", _options.ModelName);
            throw new NetGdingException(
                ErrorCodes.LlmResponseInvalid,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API returned an empty response body for model '{_options.ModelName}'.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorElement))
        {
            var errMessage = errorElement.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "Unknown LLM error message";
            var errCode = errorElement.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : "N/A";
            
            _logger.LogError(
                "LLM: API returned an error payload for model '{ModelName}'. Code: {ErrCode}, Message: {ErrMessage}, Response body: {Body}",
                _options.ModelName, errCode, errMessage, body);
                
            throw new NetGdingException(
                ErrorCodes.LlmRequestFailed,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API returned an error payload for model '{_options.ModelName}': {errMessage} (Code: {errCode})");
        }

        if (!root.TryGetProperty("choices", out var choicesElement) || choicesElement.ValueKind != JsonValueKind.Array || choicesElement.GetArrayLength() == 0)
        {
            _logger.LogError(
                "LLM: API response is missing choices array or choices array is empty for model '{ModelName}'. Response body: {Body}",
                _options.ModelName, body);
            throw new NetGdingException(
                ErrorCodes.LlmResponseInvalid,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API response structure is invalid for model '{_options.ModelName}': choices array is missing or empty.");
        }

        var firstChoice = choicesElement[0];
        if (!firstChoice.TryGetProperty("message", out var messageElement))
        {
            _logger.LogError(
                "LLM: API response first choice is missing 'message' property for model '{ModelName}'. Response body: {Body}",
                _options.ModelName, body);
            throw new NetGdingException(
                ErrorCodes.LlmResponseInvalid,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API response structure is invalid for model '{_options.ModelName}': first choice is missing 'message' property.");
        }

        if (!messageElement.TryGetProperty("content", out var contentElement))
        {
            _logger.LogError(
                "LLM: API response message is missing 'content' property for model '{ModelName}'. Response body: {Body}",
                _options.ModelName, body);
            throw new NetGdingException(
                ErrorCodes.LlmResponseInvalid,
                "LlmAnalyzer.CallChatCompletionAsync",
                $"LLM API response structure is invalid for model '{_options.ModelName}': message is missing 'content' property.");
        }

        return contentElement.GetString() ?? "";
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