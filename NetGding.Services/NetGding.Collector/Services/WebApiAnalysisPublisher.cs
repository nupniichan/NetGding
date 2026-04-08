using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public sealed class WebApiAnalysisPublisher : IAnalysisPublisher
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly ILogger<WebApiAnalysisPublisher> _logger;

    public WebApiAnalysisPublisher(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<CollectorOptions> options,
        ILogger<WebApiAnalysisPublisher> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task PublishAsync(AnalysisResult result, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;

        if (!o.WebApiPublishEnabled || string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
            return;

        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/publish";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient(nameof(WebApiAnalysisPublisher));
                var response = await http.PostAsJsonAsync(url, result, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                _logger.LogDebug(
                    "WebApiAnalysisPublisher: published {Symbol} ({Timeframe})",
                    result.Symbol, result.Timeframe);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "WebApiAnalysisPublisher: attempt {Attempt}/3 failed for {Symbol}",
                    attempt, result.Symbol);

                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }

        _logger.LogError(
            "WebApiAnalysisPublisher: all attempts failed for {Symbol} ({Timeframe}), skipping publish.",
            result.Symbol, result.Timeframe);
    }
}