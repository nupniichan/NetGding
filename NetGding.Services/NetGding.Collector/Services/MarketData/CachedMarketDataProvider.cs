using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.News;

namespace NetGding.Collector.Services.MarketData;

/// <summary>
/// Fetches and caches News + Fear &amp; Greed Index data from the WebAPI service.
/// Polls on a schedule instead of subscribing to Redis streams,
/// eliminating the circular dependency (WebAPI publishing → Collector consuming).
/// </summary>
public sealed class CachedMarketDataProvider : BackgroundService, ICachedMarketDataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly ILogger<CachedMarketDataProvider> _logger;

    private readonly ConcurrentDictionary<string, IReadOnlyList<NewsArticle>> _newsCache
        = new(StringComparer.OrdinalIgnoreCase);

    private FearAndGreedResult? _latestFearAndGreed;

    // Temporary step cache: requestId → (stepKey → object)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _tempExecutionCache = new();

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public CachedMarketDataProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CollectorOptions> options,
        ILogger<CachedMarketDataProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CachedMarketDataProvider] Starting periodic News + FearAndGreed refresh from WebAPI...");

        // Initial fetch on startup with a short delay to let WebAPI start first
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshCacheAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                // Refresh every 15 minutes (same cadence as before)
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("[CachedMarketDataProvider] Stopped.");
    }

    private async Task RefreshCacheAsync(CancellationToken ct)
    {
        var webApiBaseUrl = _options.CurrentValue.WebApiBaseUrl;
        if (string.IsNullOrWhiteSpace(webApiBaseUrl))
        {
            _logger.LogDebug("[CachedMarketDataProvider] WebApiBaseUrl not configured, skipping cache refresh.");
            return;
        }

        var http = _httpClientFactory.CreateClient(nameof(CachedMarketDataProvider));
        http.BaseAddress = new Uri(webApiBaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);

        await FetchNewsAsync(http, ct).ConfigureAwait(false);
        await FetchFearAndGreedAsync(http, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return [];

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (_newsCache.TryGetValue(normalizedSymbol, out var news))
            return news;

        // Fetch on-demand from WebAPI if not in cache yet
        var webApiBaseUrl = _options.CurrentValue.WebApiBaseUrl;
        if (!string.IsNullOrWhiteSpace(webApiBaseUrl))
        {
            try
            {
                var http = _httpClientFactory.CreateClient(nameof(CachedMarketDataProvider));
                http.BaseAddress = new Uri(webApiBaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(15);

                var articles = await FetchNewsForSymbolAsync(http, normalizedSymbol, ct).ConfigureAwait(false);
                if (articles is { Count: > 0 })
                {
                    _newsCache[normalizedSymbol] = articles;
                    return articles;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[CachedMarketDataProvider] Failed to fetch on-demand news for {Symbol}", normalizedSymbol);
            }
        }

        return [];
    }

    private async Task FetchNewsAsync(HttpClient http, CancellationToken ct)
    {
        var activeSymbols = _newsCache.Keys.ToList();
        foreach (var symbol in activeSymbols)
        {
            var articles = await FetchNewsForSymbolAsync(http, symbol, ct).ConfigureAwait(false);
            if (articles is { Count: > 0 })
            {
                _newsCache[symbol] = articles;
            }
        }
    }

    private async Task<IReadOnlyList<NewsArticle>?> FetchNewsForSymbolAsync(HttpClient http, string symbol, CancellationToken ct)
    {
        try
        {
            var response = await http.GetFromJsonAsync<WebApiNewsResponse>(
                $"api/news/{Uri.EscapeDataString(symbol)}?limit=10",
                s_json, ct).ConfigureAwait(false);

            if (response?.Items is { Count: > 0 })
            {
                return response.Items.Select(item => new NewsArticle(
                    item.Id,
                    item.Title,
                    "",
                    item.Source,
                    item.Summary,
                    item.Url,
                    item.PublishedAtUtc,
                    item.PublishedAtUtc,
                    [symbol],
                    ""
                )).ToList();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[CachedMarketDataProvider] Failed to fetch news for {Symbol} from WebAPI", symbol);
        }

        return null;
    }

    private async Task FetchFearAndGreedAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            var response = await http.GetFromJsonAsync<WebApiFearAndGreedResponse>(
                "api/fear-and-greed", s_json, ct).ConfigureAwait(false);

            if (response is not null)
            {
                _latestFearAndGreed = new FearAndGreedResult
                {
                    Value = response.Value,
                    Classification = response.Classification,
                    TimestampUtc = response.TimestampUtc
                };
                _logger.LogDebug("[CachedMarketDataProvider] Refreshed Fear & Greed: {Value} ({Class})",
                    response.Value, response.Classification);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[CachedMarketDataProvider] Failed to fetch Fear & Greed from WebAPI");
        }
    }

    public Task<FearAndGreedResult?> GetFearAndGreedAsync(CancellationToken ct = default)
        => Task.FromResult(_latestFearAndGreed);

    public void CacheTemporaryStep<T>(string requestId, string stepKey, T value)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(stepKey) || value is null)
            return;

        var reqDict = _tempExecutionCache.GetOrAdd(requestId, _ => new ConcurrentDictionary<string, object>());
        reqDict[stepKey] = value;
        _logger.LogDebug("[CachedMarketDataProvider] Cached temp step '{StepKey}' for RequestId={RequestId}", stepKey, requestId);
    }

    public T? GetTemporaryStep<T>(string requestId, string stepKey)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(stepKey))
            return default;

        if (_tempExecutionCache.TryGetValue(requestId, out var reqDict) &&
            reqDict.TryGetValue(stepKey, out var value) &&
            value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    public void ClearTemporarySteps(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        if (_tempExecutionCache.TryRemove(requestId, out _))
        {
            _logger.LogDebug("[CachedMarketDataProvider] Cleared temporary execution steps for RequestId={RequestId}", requestId);
        }
    }

    // --- DTO types for WebAPI responses ---

    private sealed record WebApiNewsResponse(
        string Symbol,
        int Count,
        List<WebApiNewsItem> Items);

    private sealed record WebApiNewsItem(
        long Id,
        string Symbol,
        string Title,
        string Source,
        string Url,
        DateTime PublishedAtUtc,
        string Summary,
        string Sentiment);

    private sealed record WebApiFearAndGreedResponse(
        int Value,
        string Classification,
        DateTime TimestampUtc);
}
