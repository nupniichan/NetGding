using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

public interface IExchangeMarketDataCollector
{
    string Exchange { get; }
    MarketType MarketType { get; }

    Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        string timeframe,
        CancellationToken ct = default);

    Task<MarketDepthDto?> GetDepthAsync(
        string symbol,
        int limit = 10,
        CancellationToken ct = default);
}
