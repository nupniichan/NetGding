using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.WebApi.Services;

public sealed class CollectorGateway : ICollectorGateway
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<CollectorGateway> _logger;

    public CollectorGateway(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<CollectorGateway> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<AnalysisNotification?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.CollectorServiceUrl))
        {
            _logger.LogError("CollectorGateway: CollectorServiceUrl is not configured.");
            return null;
        }

        var url = $"{o.CollectorServiceUrl.TrimEnd('/')}/api/analysis/on-demand";

        try
        {
            var response = await HttpRetryHelper.ExecuteAsync(
                () =>
                {
                    var http = _httpFactory.CreateClient(nameof(CollectorGateway));
                    return http.PostAsJsonAsync(url, request, ct);
                },
                maxRetries: Math.Max(1, o.MaxRetries),
                baseDelaySeconds: 2,
                onRetry: (attempt, max, status) => _logger.LogWarning(
                    "CollectorGateway: attempt {Attempt}/{Max} failed (status={Status}) for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                    attempt, max, status, request.Symbol, request.Timeframe, request.Exchange, request.MarketType),
                ct: ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AnalysisNotification>(cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CollectorGateway failed for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                request.Symbol, request.Timeframe, request.Exchange, request.MarketType);
            return null;
        }
    }

    public async Task<MarketDepthDto?> GetDepthAsync(string symbol, string exchange, string marketType, int limit, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.CollectorServiceUrl))
        {
            _logger.LogError("CollectorGateway: CollectorServiceUrl is not configured.");
            return null;
        }

        var url = $"{o.CollectorServiceUrl.TrimEnd('/')}/api/market/dom?symbol={Uri.EscapeDataString(symbol)}&exchange={Uri.EscapeDataString(exchange)}&marketType={Uri.EscapeDataString(marketType)}&limit={limit}";

        try
        {
            var response = await HttpRetryHelper.ExecuteAsync(
                () =>
                {
                    var http = _httpFactory.CreateClient(nameof(CollectorGateway));
                    return http.GetAsync(url, ct);
                },
                maxRetries: Math.Max(1, o.MaxRetries),
                baseDelaySeconds: 2,
                onRetry: (attempt, max, status) => _logger.LogWarning(
                    "CollectorGateway: get depth attempt {Attempt}/{Max} failed (status={Status}) for {Symbol}",
                    attempt, max, status, symbol),
                ct: ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MarketDepthDto>(cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CollectorGateway GetDepthAsync failed for {Symbol}", symbol);
            return null;
        }
    }
}
