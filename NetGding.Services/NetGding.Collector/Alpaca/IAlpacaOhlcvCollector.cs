using Alpaca.Markets;
using NetGding.Models.MarketData;

namespace NetGding.Collector.Alpaca;

public interface IAlpacaOhlcvCollector
{
    Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        BarTimeFrame timeFrame,
        CancellationToken cancellationToken = default);
}