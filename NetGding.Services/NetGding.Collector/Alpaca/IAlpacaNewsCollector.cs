using NetGding.Contracts.Models.News;

namespace NetGding.Collector.Alpaca;

public interface IAlpacaNewsCollector
{
    Task<IReadOnlyList<NewsArticle>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}