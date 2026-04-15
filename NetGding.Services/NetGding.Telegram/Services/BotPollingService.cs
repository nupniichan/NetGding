using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Telegram.Formatting;

namespace NetGding.Telegram.Services;

public sealed class BotPollingService : BackgroundService
{
    private const string WebApiHttpClient = "WebApiClient";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly IAnalysisStore _store;
    private readonly ITelegramNotifier _notifier;
    private readonly AnalysisMessageFormatter _formatter;
    private readonly ILogger<BotPollingService> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public BotPollingService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<TelegramOptions> options,
        IAnalysisStore store,
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
        }
    }

    private async Task HandleAnalyzeCommandAsync(string text, string chatId, CancellationToken ct)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape("Usage: /analyze <symbol> <timeframe>  e.g. /analyze BTC/USD 4h"),
                ct).ConfigureAwait(false);
            return;
        }

        var symbol = parts[1].Trim();
        var timeframe = parts[2].Trim().ToLowerInvariant();

        var allowedTimeframes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "15m", "1h", "4h", "1d", "1w", "1m"
        };
        if (!allowedTimeframes.Contains(timeframe))
        {
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape("Supported timeframes: 15m, 1h, 4h, 1d, 1w, 1m."),
                ct).ConfigureAwait(false);
            return;
        }

        await _notifier.SendTextAsync(
            chatId,
            AnalysisMessageFormatter.Escape($"Analyzing {symbol} ({timeframe})... please wait."),
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
                    notification = await FetchOnDemandAnalysisAsync(candidate, timeframe, ct).ConfigureAwait(false);
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
            await _notifier.SendTextAsync(
                chatId,
                AnalysisMessageFormatter.Escape($"Collector service is unavailable. Please try again in a moment."),
                ct).ConfigureAwait(false);
        }
    }

    private async Task<AnalysisNotification> FetchOnDemandAnalysisAsync(
        string symbol, string timeframe, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new { symbol, timeframe };

        var response = await HttpRetryHelper.ExecuteAsync(
            () => _httpFactory.CreateClient(WebApiHttpClient).PostAsJsonAsync(url, payload, ct),
            maxRetries: Math.Max(1, o.OnDemandMaxRetries),
            baseDelaySeconds: o.OnDemandRetryBaseDelaySeconds,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "BotPollingService: on-demand attempt {Attempt}/{Max} failed (status={Status}) for {Symbol} ({Timeframe})",
                attempt, max, status, symbol, timeframe),
            ct: ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var notification = await response.Content
            .ReadFromJsonAsync<AnalysisNotification>(s_jsonOptions, ct)
            .ConfigureAwait(false);

        return notification ?? throw new InvalidOperationException("WebAPI returned empty response.");
    }

    private static string BuildWelcomeMessage() =>
        "*NetGding Analysis Bot*\n\n" +
        "Available commands:\n" +
        "\\- /help \\— show available commands\n" +
        "\\- /latest `<symbol>` \\— get the cached analysis for a symbol \\(D1\\+\\)\n" +
        "\\- /analyze `<symbol>` `<timeframe>` \\— run live analysis \\(15m, 1h, 4h, 1d, 1w, 1m\\)\n\n" +
        "Examples:\n" +
        "  /analyze BTC 4h\n" +
        "  /latest BTC/USD\n\n" +
        "D1\\+ analysis results are still pushed automatically after each bar\\.";

    private sealed record TelegramUpdatesResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] TelegramUpdate[]? Result);

    private sealed record TelegramUpdate(
        [property: JsonPropertyName("update_id")] long UpdateId,
        [property: JsonPropertyName("message")] TelegramMessage? Message);

    private sealed record TelegramMessage(
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("chat")] TelegramChat? Chat);

    private sealed record TelegramChat(
        [property: JsonPropertyName("id")] long Id);
}