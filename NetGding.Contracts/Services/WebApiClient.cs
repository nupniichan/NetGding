using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;

namespace NetGding.Contracts.Services;

public sealed class WebApiClient : IWebApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<WebApiClient> _logger;

    public WebApiClient(HttpClient http, ILogger<WebApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnalysisNotification> FetchOnDemandAnalysisAsync(
        OnDemandRequest request,
        int maxRetries = 3,
        int retryBaseDelaySeconds = 2,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = "api/analysis/on-demand";

        HttpResponseMessage response;
        if (maxRetries > 1)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    response = await _http.PostAsJsonAsync(url, request, JsonDefaults.Options, ct).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode || attempt == maxRetries)
                    {
                        await EnsureSuccessOrThrowAsync(response, ct).ConfigureAwait(false);
                        var result = await response.Content
                            .ReadFromJsonAsync<AnalysisNotification>(JsonDefaults.Options, ct)
                            .ConfigureAwait(false);
                        return result ?? throw new InvalidOperationException("WebAPI returned empty analysis response.");
                    }

                    _logger.LogWarning(
                        "[WebApiClient] On-demand attempt {Attempt}/{Max} failed with status {StatusCode} for {Symbol}",
                        attempt, maxRetries, (int)response.StatusCode, request.Symbol);
                }
                catch (NetGdingException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries && !ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex,
                        "[WebApiClient] On-demand attempt {Attempt}/{Max} exception for {Symbol}",
                        attempt, maxRetries, request.Symbol);
                }

                var delayMs = (int)(Math.Pow(2, attempt - 1) * retryBaseDelaySeconds * 1000);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }

            throw new InvalidOperationException("On-demand request failed after retries.");
        }
        else
        {
            response = await _http.PostAsJsonAsync(url, request, JsonDefaults.Options, ct).ConfigureAwait(false);
            await EnsureSuccessOrThrowAsync(response, ct).ConfigureAwait(false);
            var result = await response.Content
                .ReadFromJsonAsync<AnalysisNotification>(JsonDefaults.Options, ct)
                .ConfigureAwait(false);
            return result ?? throw new InvalidOperationException("WebAPI returned empty analysis response.");
        }
    }

    public async Task<IReadOnlyList<NewsItem>> FetchNewsAsync(
        string symbol,
        int limit = 5,
        CancellationToken ct = default)
    {
        var url = $"api/news/{Uri.EscapeDataString(symbol)}?limit={limit}";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        await EnsureSuccessOrThrowAsync(response, ct).ConfigureAwait(false);

        var payload = await response.Content
            .ReadFromJsonAsync<NewsResponse>(JsonDefaults.Options, ct)
            .ConfigureAwait(false);

        return payload?.Items ?? Array.Empty<NewsItem>();
    }

    public async Task<FearAndGreedResult> FetchFearAndGreedAsync(CancellationToken ct = default)
    {
        var url = "api/fear-and-greed";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        await EnsureSuccessOrThrowAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<FearAndGreedResult>(JsonDefaults.Options, ct)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("WebAPI returned empty Fear & Greed response.");
    }

    public async Task<MarketDepthDto?> FetchDomAsync(
        string symbol,
        string exchange,
        string marketType,
        int limit = 10,
        CancellationToken ct = default)
    {
        var url = $"api/market/dom?symbol={Uri.EscapeDataString(symbol)}&exchange={Uri.EscapeDataString(exchange)}&marketType={Uri.EscapeDataString(marketType)}&limit={limit}";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await EnsureSuccessOrThrowAsync(response, ct).ConfigureAwait(false);
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<MarketDepthDto>(JsonDefaults.Options, ct)
            .ConfigureAwait(false);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        ErrorResponse? errResp = null;
        try
        {
            errResp = await response.Content
                .ReadFromJsonAsync<ErrorResponse>(JsonDefaults.Options, ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // Ignore JSON parse exception to fall back to EnsureSuccessStatusCode
        }

        if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
        {
            throw new NetGdingException(errResp.ErrorCode, errResp.Location, errResp.Message);
        }

        response.EnsureSuccessStatusCode();
    }
}
