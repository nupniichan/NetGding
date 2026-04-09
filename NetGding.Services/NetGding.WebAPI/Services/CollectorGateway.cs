using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

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

    public async Task<AnalysisResult?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.CollectorServiceUrl))
        {
            _logger.LogError("CollectorGateway: CollectorServiceUrl is not configured.");
            return null;
        }

        var url = $"{o.CollectorServiceUrl.TrimEnd('/')}/api/analysis/on-demand";
        var maxRetries = Math.Max(1, o.MaxRetries);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient(nameof(CollectorGateway));
                var response = await http.PostAsJsonAsync(url, request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<AnalysisResult>(cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "CollectorGateway: attempt {Attempt}/{MaxRetries} failed for {Symbol} ({Timeframe})",
                    attempt, maxRetries, request.Symbol, request.Timeframe);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CollectorGateway failed for {Symbol} ({Timeframe})",
                    request.Symbol, request.Timeframe);
                return null;
            }
        }

        return null;
    }
}
