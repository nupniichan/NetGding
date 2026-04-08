using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Models.MarketData;
using NetGding.Collector.Alpaca;
using NetGding.Configurations.Options;
using NetGding.Collector.Persistence;

namespace NetGding.Collector.Workers;

public sealed class CollectorWorker : BackgroundService
{
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IAlpacaOhlcvCollector _collector;
    private readonly ILogger<CollectorWorker> _logger;

    public CollectorWorker(
        IOptionsMonitor<CollectorOptions> options,
        IAlpacaOhlcvCollector collector,
        ILogger<CollectorWorker> logger)
    {
        _options = options;
        _collector = collector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;
            if (!string.IsNullOrWhiteSpace(o.ApiKey) && !string.IsNullOrWhiteSpace(o.ApiSecret))
                break;

            _logger.LogWarning(
                "Collector: set ApiKey and ApiSecret (env Alpaca_ApiKey / Alpaca_ApiSecret).");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
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
            _logger.LogWarning("Collector: no auto-scheduled BarTimeFrames (>= D1) configured.");
            return;
        }

        await Task.WhenAll(timeFrames.Select(tf => CollectLoopAsync(tf, stoppingToken)))
            .ConfigureAwait(false);
    }

    private async Task CollectLoopAsync(string tfName, CancellationToken stoppingToken)
    {
        if (!BarTimeFrameResolver.TryResolve(tfName, out var tf))
        {
            _logger.LogError(
                "Collector: invalid BarTimeFrame '{Frame}' (allowed: 15m, 1h, 4h, 1d, 1w, 1m)",
                tfName);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;
            var boundaryWait = BarTimeFrameResolver.DelayUntilNextBarBoundaryUtc(tf, DateTime.UtcNow);

            _logger.LogInformation(
                "Collector [{TimeFrame}]: next bar in {Wait:mm\\:ss}",
                tfName, boundaryWait);

            await Task.Delay(boundaryWait, stoppingToken).ConfigureAwait(false);

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-Math.Max(1, o.LookbackDays));
            var symbols = o.Symbols ?? [];

            if (symbols.Length == 0)
            {
                _logger.LogWarning("Collector: Symbols is empty; add entries in config.");
                continue;
            }

            foreach (var raw in symbols)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var symbol = raw.Trim();
                try
                {
                    IReadOnlyList<OhlcvBar> bars = await _collector
                        .CollectAsync(symbol, fromUtc, toUtc, tf, stoppingToken)
                        .ConfigureAwait(false);

                    var series = new OhlcvSeries(symbol, tfName, bars);

                    _logger.LogInformation(
                        "Collector: {Symbol} [{TimeFrame}] → {Count} bars ({From:O} … {To:O})",
                        symbol, tfName, series.Bars.Count, fromUtc, toUtc);

                    await JsonPersistence.SaveAsync(
                        o.OutputDirectory, symbol, $"ohlcv_{tfName}", series, _logger)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Collector: failed for {Symbol} [{TimeFrame}]", symbol, tfName);
                }
            }
        }
    }
}
