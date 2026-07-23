using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;
using NetGding.Telegram.Formatting;

using NetGding.Contracts.Services;

namespace NetGding.Telegram.Services;

public sealed class BotPollingService : BackgroundService
{
    private const string WebApiHttpClient = "WebApiClient";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly IAnalysisCache _store;
    private readonly ITelegramNotifier _notifier;
    private readonly AnalysisMessageFormatter _formatter;
    private readonly ILogger<BotPollingService> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly HashSet<string> s_allowedTimeframes = new(StringComparer.OrdinalIgnoreCase)
    {
        "15m", "1h", "4h", "1d", "1w", "1m"
    };
    private static readonly HashSet<string> s_allowedExchanges = new(StringComparer.OrdinalIgnoreCase) { "binance", "okx" };
    private static readonly HashSet<string> s_allowedMarketTypes = new(StringComparer.OrdinalIgnoreCase) { "spot" };


    public BotPollingService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<TelegramOptions> options,
        IAnalysisCache store,
        ITelegramNotifier notifier,
        AnalysisMessageFormatter formatter,
        ILogger<BotPollingService> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _store = store;
        _notifier = notifier;
        _formatter = formatter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            _logger.LogWarning("BotPollingService: BotToken is not configured. Polling disabled.");
            return;
        }

        _logger.LogInformation("BotPollingService: starting long-poll loop.");

        long offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                offset = await PollOnceAsync(offset, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var retryDelay = _options.CurrentValue.PollingErrorRetrySeconds;
                _logger.LogError(ex, "BotPollingService: polling error, retrying in {Delay}s.", retryDelay);
                await Task.Delay(TimeSpan.FromSeconds(retryDelay), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<long> PollOnceAsync(long offset, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var url = $"{o.ApiBaseUrl.TrimEnd('/')}/bot{o.BotToken}/getUpdates" +
                  $"?offset={offset}&timeout={o.PollingTimeoutSeconds}&allowed_updates=[\"message\"]";

        var http = _httpFactory.CreateClient(nameof(TelegramNotifier));

        using var response = await http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("BotPollingService: getUpdates returned {StatusCode}", (int)response.StatusCode);
            return offset;
        }

        var updates = await response.Content
            .ReadFromJsonAsync<TelegramUpdatesResponse>(s_jsonOptions, ct)
            .ConfigureAwait(false);

        if (updates?.Result is not { Length: > 0 } results)
            return offset;

        var newOffset = offset;

        foreach (var update in results)
        {
            newOffset = Math.Max(newOffset, update.UpdateId + 1);

            var text = update.Message?.Text;
            var chatId = update.Message?.Chat?.Id.ToString();

            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(chatId))
                continue;

            await HandleCommandAsync(text.Trim(), chatId, ct).ConfigureAwait(false);
        }

        return newOffset;
    }

    private async Task HandleCommandAsync(string text, string chatId, CancellationToken ct)
    {
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await _notifier.SendTextAsync(chatId, BuildWelcomeMessage(), ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/fagi", StringComparison.OrdinalIgnoreCase))
        {
            await HandleFagiCommandAsync(chatId, ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/latest", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                await _notifier.SendTextAsync(
                    chatId,
                    AnalysisMessageFormatter.Escape("Usage: /latest <symbol>  e.g. /latest BTC/USD"),
                    ct).ConfigureAwait(false);
                return;
            }

            var symbol = parts[1].Trim();
            var result = _store.GetLatest(symbol);

            if (result is null)
            {
                await _notifier.SendTextAsync(
                    chatId,
                    AnalysisMessageFormatter.Escape($"No analysis found for symbol: {symbol}"),
                    ct).ConfigureAwait(false);
                return;
            }

            var message = _formatter.Build(result);
            await _notifier.SendTextAsync(chatId, message, ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/analyze", StringComparison.OrdinalIgnoreCase))
        {
            await HandleAnalyzeCommandAsync(text, chatId, ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/chart", StringComparison.OrdinalIgnoreCase))
        {
            await HandleChartCommandAsync(text, chatId, ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/news", StringComparison.OrdinalIgnoreCase))
        {
            await HandleNewsCommandAsync(text, chatId, ct).ConfigureAwait(false);
            return;
        }

        if (text.StartsWith("/dom", StringComparison.OrdinalIgnoreCase))
        {
            await HandleDomCommandAsync(text, chatId, ct).ConfigureAwait(false);
            return;
        }
    }

    private async Task HandleAnalyzeCommandAsync(string text, string chatId, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape("Usage: /analyze <symbol> <timeframe> [<exchange>]  e.g. /analyze BTC 4h (default exchange: binance)"),
                ct).ConfigureAwait(false);
            return;
        }

        var symbol = parts[1].Trim();
        var timeframe = parts[2].Trim().ToLowerInvariant();
        var exchange = parts.Length > 3 ? parts[3].Trim().ToLowerInvariant() : "binance";
        const string marketType = "spot";

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape("Supported timeframes: 15m, 1h, 4h, 1d, 1w, 1m."),
                ct).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape("Supported exchanges: binance, okx."),
                ct).ConfigureAwait(false);
            return;
        }

        await _notifier.SendTextAsync(
            chatId,
            AnalysisMessageFormatter.Escape($"Analyzing {symbol} ({timeframe}, {exchange})... please wait."),
            ct).ConfigureAwait(false);

        try
        {
            var symbolCandidates = symbol.Contains('/', StringComparison.Ordinal)
                ? [symbol]
                : symbol.EndsWith("/USD", StringComparison.OrdinalIgnoreCase)
                    ? [symbol]
                    : new[] { symbol, $"{symbol}/USD" };

            AnalysisNotification? notification = null;
            Exception? lastError = null;

            foreach (var candidate in symbolCandidates)
            {
                try
                {
                    notification = await FetchOnDemandAnalysisAsync(candidate, timeframe, exchange, marketType, null, false, ct)
                        .ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (notification is null)
                throw lastError ?? new InvalidOperationException("On-demand analysis failed.");

            _store.Store(notification.Result);
            await _notifier.SendAnalysisAsync(notification, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotPollingService: on-demand analysis failed for {Symbol} ({Timeframe})", symbol, timeframe);
            await SendFormattedErrorAsync(chatId, "Analysis Failed", ex, ct).ConfigureAwait(false);
        }
    }

    private async Task<AnalysisNotification> FetchOnDemandAnalysisAsync(
        string symbol,
        string timeframe,
        string exchange,
        string marketType,
        string? chartSymbol,
        bool chartOnly,
        CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new { symbol, timeframe, exchange, marketType, chartSymbol, chartOnly };

        var response = await HttpRetryHelper.ExecuteAsync(
            () => _httpFactory.CreateClient(WebApiHttpClient).PostAsJsonAsync(url, payload, ct),
            maxRetries: Math.Max(1, o.OnDemandMaxRetries),
            baseDelaySeconds: o.OnDemandRetryBaseDelaySeconds,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "BotPollingService: on-demand attempt {Attempt}/{Max} failed (status={Status}) for {Symbol} ({Timeframe})",
                attempt, max, status, symbol, timeframe),
            ct: ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions, ct).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }
            response.EnsureSuccessStatusCode();
        }

        var notification = await response.Content
            .ReadFromJsonAsync<AnalysisNotification>(s_jsonOptions, ct)
            .ConfigureAwait(false);

        return notification ?? throw new InvalidOperationException("WebAPI returned empty response.");
    }

    private async Task HandleFagiCommandAsync(string chatId, CancellationToken ct)
    {
        try
        {
            var o = _options.CurrentValue;
            var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/fear-and-greed";
            var http = _httpFactory.CreateClient(WebApiHttpClient);
            var fng = await http.GetFromJsonAsync<FearAndGreedResult>(url, ct).ConfigureAwait(false);

            if (fng is null)
            {
                await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Failed to fetch Fear & Greed Index."), ct).ConfigureAwait(false);
                return;
            }

            var emoji = AnalysisMessageFormatter.GetFearAndGreedEmoji(fng.Value);
            var msg = $"*Crypto Fear & Greed Index*\n\n" +
                      $"*Value:* {fng.Value}\n" +
                      $"*Classification:* {emoji} {fng.Classification}\n" +
                      $"*Updated:* {AnalysisMessageFormatter.Escape(fng.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"))} UTC";

            await _notifier.SendTextAsync(chatId, msg, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotPollingService: failed to handle /fagi command");
            await SendFormattedErrorAsync(chatId, "FAGI Command Failed", ex, ct).ConfigureAwait(false);
        }
    }

    private static string BuildWelcomeMessage() =>
        "*NetGding Analysis Bot*\n\n" +
        "Available commands:\n" +
        "\\- /help \\— show available commands\n" +
        "\\- /latest `<symbol>` \\— get the cached analysis for a symbol \\(D1\\+\\)\n" +
        "\\- /analyze `<symbol>` `<timeframe>` `[<exchange>]` `[<market_type>]` \\— run live analysis \\(15m, 1h, 4h, 1d, 1w, 1m, defaults: binance, spot\\)\n" +
        "\\- /chart `[<symbol>]` `[<timeframe>]` `[<exchange>]` `[<market_type>]` \\— get live chart \\(defaults: BTC, 4h, binance, spot\\)\n" +
        "\\- /news `[<symbol>]` `[<limit>]` \\— get recent news articles \\(defaults: BTC, 5\\)\n" +
        "\\- /dom `[<timeframe>]` \\— check BTC dominance chart and DOM \\(default: 4h\\)\n" +
        "\\- /fagi \\— get the current Crypto Fear and Greed Index\n\n" +
        "Indicator legend \\(shown on chart and legend\\):\n" +
        "\\- EMAx \\— Exponential Moving Average\n" +
        "\\- BB \\— Bollinger Bands\n" +
        "\\- VWAP \\— Volume Weighted Average Price\n" +
        "\\- S/R \\— Support/Resistance levels\n" +
        "\\- Entry/SL/TP/Buy \\— Risk management price levels\n\n" +
        "Examples:\n" +
        "  /analyze BTC 4h\n" +
        "  /chart BTC 4h\n" +
        "  /dom 4h\n" +
        "  /news BTC 5\n" +
        "  /latest BTC/USD\n\n" +
        "D1\\+ analysis results are still pushed automatically after each bar\\.";

    private async Task HandleChartCommandAsync(string text, string chatId, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var symbol = parts.Length > 1 ? parts[1].Trim() : "BTC";
        var timeframe = parts.Length > 2 ? parts[2].Trim().ToLowerInvariant() : "4h";
        var exchange = parts.Length > 3 ? parts[3].Trim().ToLowerInvariant() : "binance";
        const string marketType = "spot";

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Supported timeframes: 15m, 1h, 4h, 1d, 1w, 1m."), ct).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Supported exchanges: binance, okx."), ct).ConfigureAwait(false);
            return;
        }

        var normalizedSymbol = symbol.Contains('/', StringComparison.Ordinal) ? symbol : $"{symbol.ToUpperInvariant()}/USD";

        await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape($"Fetching chart for {normalizedSymbol} ({timeframe}, {exchange})... please wait."), ct).ConfigureAwait(false);

        try
        {
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe, exchange, marketType, null, true, ct).ConfigureAwait(false);

            var r = notification.Result;
            var captionBuilder = new StringBuilder();
            captionBuilder.Append("*NetGding Chart* \\| *").Append(AnalysisMessageFormatter.Escape(r.Symbol)).Append("* \\| *").Append(AnalysisMessageFormatter.Escape(r.Timeframe.ToUpperInvariant())).Append("*\n\n");
            captionBuilder.Append("*Price:* ").Append(AnalysisMessageFormatter.Escape(r.CurrentPrice.ToString("F2"))).Append("\n");
            
            if (r.Reason != "Chart Only")
            {
                captionBuilder.Append("*Decision:* ").Append(AnalysisMessageFormatter.Escape(r.Decision.ToString().ToUpperInvariant())).Append(" \\(").Append($"{(r.Confidence * 100):F0}").Append("%\\)\n");
                captionBuilder.Append("*Hold Time:* ").Append(AnalysisMessageFormatter.Escape(string.IsNullOrWhiteSpace(r.ExpectedHoldTime) ? "N/A" : r.ExpectedHoldTime)).Append("\n");
            }
            
            captionBuilder.Append("*Datetime:* ").Append(AnalysisMessageFormatter.Escape(r.AnalyzedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(" UTC");
            var caption = captionBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(notification.ChartImageBase64))
            {
                var chartBytes = Convert.FromBase64String(notification.ChartImageBase64);
                await _notifier.SendPhotoAsync(chatId, chartBytes, "chart.png", caption, ct).ConfigureAwait(false);
            }
            else
            {
                await _notifier.SendTextAsync(chatId, caption, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotPollingService: chart fetch failed for {Symbol} ({Timeframe})", normalizedSymbol, timeframe);
            await SendFormattedErrorAsync(chatId, "Chart Generation Failed", ex, ct).ConfigureAwait(false);
        }
    }

    private async Task HandleNewsCommandAsync(string text, string chatId, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var symbol = parts.Length > 1 ? parts[1].Trim() : "BTC";
        var limitStr = parts.Length > 2 ? parts[2].Trim() : "5";
        if (!int.TryParse(limitStr, out var limit)) limit = 5;
        limit = Math.Clamp(limit, 1, 10);

        var normalizedSymbol = symbol.Contains('/', StringComparison.Ordinal) ? symbol : $"{symbol.ToUpperInvariant()}/USD";

        await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape($"Fetching recent news for {normalizedSymbol}... please wait."), ct).ConfigureAwait(false);

        try
        {
            var articles = await FetchNewsAsync(normalizedSymbol, limit, ct).ConfigureAwait(false);

            var sb = new StringBuilder();
            sb.Append("*NetGding News* \\| *").Append(AnalysisMessageFormatter.Escape(normalizedSymbol.ToUpperInvariant())).Append("*\n\n");

            if (articles.Count == 0)
            {
                sb.Append("No recent news articles found for this symbol\\.");
            }
            else
            {
                foreach (var art in articles)
                {
                    var sentimentEmoji = art.Sentiment?.ToLowerInvariant() switch
                    {
                        "bullish" or "positive" => "🟢",
                        "bearish" or "negative" => "🔴",
                        _ => "⚪"
                    };

                    var escapedTitle = AnalysisMessageFormatter.Escape(art.Title);
                    var escapedSource = AnalysisMessageFormatter.Escape(art.Source);
                    var escapedSentiment = AnalysisMessageFormatter.Escape(art.Sentiment ?? "Neutral");
                    var escapedSummary = AnalysisMessageFormatter.Escape(art.Summary.Length > 200 ? art.Summary[..197] + "..." : art.Summary);
                    var escapedUrl = art.Url.Replace("\\", "\\\\").Replace(")", "\\)");

                    sb.Append("📰 *[").Append(escapedTitle).Append("](").Append(escapedUrl).Append(")*\n");
                    sb.Append("Source: ").Append(escapedSource).Append(" \\| ").Append(sentimentEmoji).Append(" ").Append(escapedSentiment).Append("\n");
                    sb.Append(escapedSummary).Append("\n\n");
                }
            }

            await _notifier.SendTextAsync(chatId, sb.ToString(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotPollingService: news fetch failed for {Symbol}", normalizedSymbol);
            await SendFormattedErrorAsync(chatId, "News Fetch Failed", ex, ct).ConfigureAwait(false);
        }
    }

    private async Task HandleDomCommandAsync(string text, string chatId, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int argIndex = 1;
        if (parts.Length > argIndex && (parts[argIndex].Equals("btc", StringComparison.OrdinalIgnoreCase) || 
                                        parts[argIndex].Equals("btc/usd", StringComparison.OrdinalIgnoreCase) || 
                                        parts[argIndex].Equals("btcusd", StringComparison.OrdinalIgnoreCase)))
        {
            argIndex++;
        }

        var timeframe = parts.Length > argIndex ? parts[argIndex].Trim().ToLowerInvariant() : "4h";
        var exchange = "binance";
        var marketType = "spot";

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Supported timeframes: 15m, 1h, 4h, 1d, 1w, 1m."), ct).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Supported exchanges: binance, okx."), ct).ConfigureAwait(false);
            return;
        }

        if (!s_allowedMarketTypes.Contains(marketType))
        {
            await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape("Supported market types: spot, future."), ct).ConfigureAwait(false);
            return;
        }

        var normalizedSymbol = "BTC/USD";

        await _notifier.SendTextAsync(chatId, AnalysisMessageFormatter.Escape($"Fetching DOM and chart for {normalizedSymbol}... please wait."), ct).ConfigureAwait(false);

        try
        {
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe, exchange, marketType, "CRYPTOCAP:BTC.D", true, ct).ConfigureAwait(false);

            var r = notification.Result;
            var sb = new StringBuilder();
            sb.Append("*NetGding DOM Chart* \\| *").Append(AnalysisMessageFormatter.Escape(r.Symbol)).Append("* \\| *").Append(AnalysisMessageFormatter.Escape(r.Timeframe.ToUpperInvariant())).Append("*\n\n");
            sb.Append("*Datetime:* ").Append(AnalysisMessageFormatter.Escape(r.AnalyzedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(" UTC");

            if (!string.IsNullOrWhiteSpace(notification.ChartImageBase64))
            {
                var chartBytes = Convert.FromBase64String(notification.ChartImageBase64);
                await _notifier.SendPhotoAsync(chatId, chartBytes, "chart.png", sb.ToString(), ct).ConfigureAwait(false);
            }
            else
            {
                await _notifier.SendTextAsync(chatId, sb.ToString(), ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotPollingService: DOM/chart request failed for {Symbol}", normalizedSymbol);
            await SendFormattedErrorAsync(chatId, "DOM Request Failed", ex, ct).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<TelegramNewsItem>> FetchNewsAsync(string symbol, int limit, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/news/{Uri.EscapeDataString(symbol)}?limit={limit}";

        var client = _httpFactory.CreateClient(WebApiHttpClient);
        var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions, ct).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<TelegramNewsResponse>(s_jsonOptions, ct).ConfigureAwait(false);
        return payload?.Items ?? Array.Empty<TelegramNewsItem>();
    }

    private async Task<MarketDepthDto?> FetchDomAsync(string symbol, string exchange, string marketType, int limit, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/market/dom?symbol={Uri.EscapeDataString(symbol)}&exchange={Uri.EscapeDataString(exchange)}&marketType={Uri.EscapeDataString(marketType)}&limit={limit}";

        var client = _httpFactory.CreateClient(WebApiHttpClient);
        var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions, ct).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MarketDepthDto>(s_jsonOptions, ct).ConfigureAwait(false);
    }

    private async Task SendFormattedErrorAsync(string chatId, string title, Exception ex, CancellationToken ct)
    {
        var code = ErrorCodes.Unknown;
        var location = "BotPollingService";
        var message = ex.Message;

        if (ex is NetGding.Contracts.Exceptions.NetGdingException nex)
        {
            code = nex.ErrorCode;
            location = nex.Location;
            message = nex.Message;
        }
        else if (ex.InnerException is NetGding.Contracts.Exceptions.NetGdingException inex)
        {
            code = inex.ErrorCode;
            location = inex.Location;
            message = inex.Message;
        }

        var escapedCode = AnalysisMessageFormatter.Escape(code);
        var escapedLoc = AnalysisMessageFormatter.Escape(location);
        var escapedMsg = AnalysisMessageFormatter.Escape(message);
        var errorMsg = $"❌ *{title}*\n• *Code:* `{escapedCode}`\n• *Location:* `{escapedLoc}`\n• *Message:* {escapedMsg}";

        await _notifier.SendTextAsync(chatId, errorMsg, ct).ConfigureAwait(false);
    }
}