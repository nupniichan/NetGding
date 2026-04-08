using Alpaca.Markets;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Models.News;

namespace NetGding.Collector.Alpaca;

public sealed class AlpacaNewsCollector : IAlpacaNewsCollector
{
    private readonly IAlpacaDataClient _dataClient;
    private readonly ILogger<AlpacaNewsCollector> _logger;

    public AlpacaNewsCollector(
        IAlpacaDataClient dataClient,
        ILogger<AlpacaNewsCollector> logger)
    {
        _dataClient = dataClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsArticle>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var request = new NewsArticlesRequest([symbol])
        {
            TimeInterval = new Interval<DateTime>(fromUtc, toUtc),
            SortDirection = SortDirection.Descending
        };

        IPage<INewsArticle> page;
        try
        {
            page = await _dataClient.ListNewsArticlesAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch news for {Symbol}", symbol);
            return Array.Empty<NewsArticle>();
        }

        if (page.Items.Count == 0)
        {
            _logger.LogDebug("No news articles returned for {Symbol}", symbol);
            return Array.Empty<NewsArticle>();
        }

        var articles = new List<NewsArticle>(page.Items.Count);
        foreach (var item in page.Items)
            articles.Add(NewsArticleMapper.FromAlpaca(item));

        return articles;
    }
}
