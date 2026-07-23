using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.WebApi.Services;

/// <summary>
/// Calls Collector service directly over HTTP for on-demand analysis.
/// Replaces the previous Redis request/response pattern with a simple, reliable HTTP proxy.
/// </summary>
public sealed class CollectorHttpClient : ICollectorGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<CollectorHttpClient> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public CollectorHttpClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<CollectorHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnalysisNotification?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.CollectorServiceUrl))
            throw new NetGdingException(
                ErrorCodes.CollectorGatewayFailed,
                "CollectorHttpClient.AnalyzeOnDemandAsync",
                "CollectorServiceUrl is not configured.");

        _logger.LogInformation("[CollectorHttpClient] Sending on-demand analysis to Collector for {Symbol} ({Timeframe})",
            request.Symbol, request.Timeframe);

        var http = _httpClientFactory.CreateClient(nameof(CollectorHttpClient));
        http.BaseAddress = new Uri(o.CollectorServiceUrl.TrimEnd('/') + "/");

        var timeoutSeconds = Math.Max(30, o.CollectorTimeoutSeconds);
        http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        try
        {
            var json = JsonSerializer.Serialize(request, s_jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await http.PostAsync("api/analysis/on-demand", content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("[CollectorHttpClient] Collector returned {StatusCode} for {Symbol}: {Body}",
                    (int)response.StatusCode, request.Symbol, body);

                // Try deserialize structured error response
                try
                {
                    var err = JsonSerializer.Deserialize<ErrorResponse>(body, s_jsonOptions);
                    if (err is not null)
                        throw new NetGdingException(err.ErrorCode, err.Location, err.Message);
                }
                catch (JsonException) { /* fall through */ }

                throw new NetGdingException(
                    ErrorCodes.CollectorGatewayFailed,
                    "CollectorHttpClient.AnalyzeOnDemandAsync",
                    $"Collector service returned HTTP {(int)response.StatusCode}.");
            }

            var notification = await response.Content.ReadFromJsonAsync<AnalysisNotification>(s_jsonOptions, ct).ConfigureAwait(false);
            return notification;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError("[CollectorHttpClient] Timeout waiting for Collector response for {Symbol} after {Timeout}s",
                request.Symbol, timeoutSeconds);
            throw new NetGdingException(
                ErrorCodes.CollectorGatewayFailed,
                "CollectorHttpClient.AnalyzeOnDemandAsync",
                $"Timeout waiting for Collector response after {timeoutSeconds}s.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CollectorHttpClient] HTTP error calling Collector for {Symbol}", request.Symbol);
            throw new NetGdingException(
                ErrorCodes.CollectorGatewayFailed,
                "CollectorHttpClient.AnalyzeOnDemandAsync",
                $"HTTP error calling Collector service: {ex.Message}", ex);
        }
    }

    public async Task<MarketDepthDto?> GetDepthAsync(string symbol, string exchange, string marketType, int limit, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.CollectorServiceUrl))
            return null;

        var http = _httpClientFactory.CreateClient(nameof(CollectorHttpClient));
        http.BaseAddress = new Uri(o.CollectorServiceUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(Math.Max(10, o.HealthTimeoutSeconds));

        try
        {
            var url = $"api/market/dom?symbol={Uri.EscapeDataString(symbol)}&exchange={Uri.EscapeDataString(exchange)}&marketType={Uri.EscapeDataString(marketType)}&limit={limit}";
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<MarketDepthDto>(s_jsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CollectorHttpClient] Failed to fetch DOM for {Symbol}", symbol);
            return null;
        }
    }
}
