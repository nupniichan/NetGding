using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Models.MarketData;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Configuration;

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
            if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.ApiSecret))
            {
                _logger.LogWarning(
                    "Collector: set ApiKey and ApiSecret (User Secrets or env Collector__ApiKey / Collector__ApiSecret).");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (!BarTimeFrameResolver.TryResolve(o.BarTimeFrame, out var tf))
            {
                _logger.LogError(
                    "Collector: invalid BarTimeFrame '{Frame}' (allowed: 15m, 1h, 4h, 1D, 1W, 1M)",
                    o.BarTimeFrame);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-Math.Max(1, o.LookbackDays));
            try
            {
                IReadOnlyList<OhlcvBar> bars = await _collector
                    .CollectAsync(o.Symbol, fromUtc, toUtc, tf, stoppingToken)
                    .ConfigureAwait(false);
                var series = new OhlcvSeries(o.Symbol, o.BarTimeFrame, bars);
                _logger.LogInformation(
                    "Collector: {Symbol} → {Count} bars ({From:O} … {To:O})",
                    series.Symbol,
                    series.Bars.Count,
                    fromUtc,
                    toUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Collector: failed for {Symbol}", o.Symbol);
            }

            var delay = TimeSpan.FromSeconds(Math.Max(10, o.PollIntervalSeconds));
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}