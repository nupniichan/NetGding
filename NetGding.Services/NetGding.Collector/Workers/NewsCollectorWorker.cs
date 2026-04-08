using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.News;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Configuration;
using NetGding.Collector.Persistence;

namespace NetGding.Collector.Workers;

public sealed class NewsCollectorWorker : BackgroundService
{
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IAlpacaNewsCollector _newsCollector;
    private readonly ILogger<NewsCollectorWorker> _logger;

    public NewsCollectorWorker(
        IOptionsMonitor<CollectorOptions> options,
        IAlpacaNewsCollector newsCollector,
        ILogger<NewsCollectorWorker> logger)
    {
        _options = options;
        _newsCollector = newsCollector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;

            if (!o.NewsEnabled)
            {
                _logger.LogDebug("News collection is disabled.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.ApiSecret))
            {
                _logger.LogWarning(
                    "NewsCollector: set ApiKey and ApiSecret.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddHours(-Math.Max(1, o.NewsLookbackHours));
            var symbols = o.Symbols ?? [];

            foreach (var raw in symbols)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var symbol = raw.Trim();
                try
                {
                    var articles = await _newsCollector
                        .CollectAsync(symbol, fromUtc, toUtc, stoppingToken)
                        .ConfigureAwait(false);

                    var collection = new NewsCollection(symbol, articles);

                    _logger.LogInformation(
                        "NewsCollector: {Symbol} → {Count} articles ({From:O} … {To:O})",
                        symbol, articles.Count, fromUtc, toUtc);

                    await JsonPersistence.SaveAsync(
                        o.OutputDirectory, symbol, "news", collection, _logger)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NewsCollector: failed for {Symbol}", symbol);
                }
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, o.NewsPollingIntervalMinutes));
            await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
        }
    }
}