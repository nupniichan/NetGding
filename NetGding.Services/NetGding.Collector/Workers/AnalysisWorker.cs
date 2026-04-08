using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Services;
using NetGding.Configurations.Options;

namespace NetGding.Collector.Workers;

public sealed class AnalysisWorker : BackgroundService
{
    private static readonly TimeSpan CollectionOffset = TimeSpan.FromSeconds(30);

    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IOnDemandAnalyzer _analyzer;
    private readonly IAnalysisPublisher _publisher;
    private readonly ILogger<AnalysisWorker> _logger;

    public AnalysisWorker(
        IOptionsMonitor<CollectorOptions> options,
        IOnDemandAnalyzer analyzer,
        IAnalysisPublisher publisher,
        ILogger<AnalysisWorker> logger)
    {
        _options = options;
        _analyzer = analyzer;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(CollectionOffset, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;

            if (!o.AnalysisEnabled)
            {
                _logger.LogDebug("AI analysis is disabled.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (string.IsNullOrWhiteSpace(o.GemmaApiKey) &&
                o.GemmaBaseUrl.Contains("openrouter", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("AnalysisWorker: GemmaApiKey is required for OpenRouter.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            break;
        }

        if (stoppingToken.IsCancellationRequested) return;

        var opts = _options.CurrentValue;
        var timeFrames = (opts.BarTimeFrames ?? [])
            .Where(tf => !string.IsNullOrWhiteSpace(tf))
            .Select(tf => tf.Trim())
            .Where(BarTimeFrameResolver.IsAutoScheduled)
            .ToArray();

        if (timeFrames.Length == 0)
        {
            _logger.LogWarning("AnalysisWorker: no auto-scheduled BarTimeFrames (>= D1) configured.");
            return;
        }

        await Task.WhenAll(timeFrames.Select(tf => AnalyzeLoopAsync(tf, stoppingToken)))
            .ConfigureAwait(false);
    }

    private async Task AnalyzeLoopAsync(string tfName, CancellationToken stoppingToken)
    {
        if (!BarTimeFrameResolver.TryResolve(tfName, out var tf))
        {
            _logger.LogError(
                "AnalysisWorker: invalid BarTimeFrame '{Frame}' (allowed: 1d, 1w, 1m)",
                tfName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;
            var boundaryWait = BarTimeFrameResolver.DelayUntilNextBarBoundaryUtc(tf, DateTime.UtcNow);
            await Task.Delay(boundaryWait + CollectionOffset, stoppingToken).ConfigureAwait(false);

            var symbols = (o.Symbols ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();

            var delay = TimeSpan.FromSeconds(Math.Max(5, o.AutoAnalysisDelaySeconds));

            for (var i = 0; i < symbols.Length; i++)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var symbol = symbols[i];
                try
                {
                    var result = await _analyzer.AnalyzeAsync(symbol, tfName, stoppingToken).ConfigureAwait(false);
                    await _publisher.PublishAsync(result, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AnalysisWorker: failed for {Symbol} [{TimeFrame}]", symbol, tfName);
                }

                if (i < symbols.Length - 1)
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
