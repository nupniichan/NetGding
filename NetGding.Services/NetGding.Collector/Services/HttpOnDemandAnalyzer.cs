using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public sealed class HttpOnDemandAnalyzer : IOnDemandAnalyzer
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly ILogger<HttpOnDemandAnalyzer> _logger;

    public HttpOnDemandAnalyzer(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<CollectorOptions> options,
        ILogger<HttpOnDemandAnalyzer> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
            throw new InvalidOperationException("Collector: WebApiBaseUrl is required for HTTP on-demand analysis.");

        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new OnDemandRequest(symbol, timeframe);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient(nameof(HttpOnDemandAnalyzer));
                var response = await http.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<AnalysisResult>(cancellationToken: ct)
                    .ConfigureAwait(false);

                return result ?? throw new InvalidOperationException("Collector: empty analysis response from WebAPI.");
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex,
                    "HttpOnDemandAnalyzer: attempt {Attempt}/3 failed for {Symbol} ({Timeframe})",
                    attempt, symbol, timeframe);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"Collector: all retries failed for HTTP on-demand analysis ({symbol}, {timeframe}).");
    }
}