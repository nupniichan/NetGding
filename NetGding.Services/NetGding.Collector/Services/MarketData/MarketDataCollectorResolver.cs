using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

public sealed class MarketDataCollectorResolver : IMarketDataCollectorResolver
{
    private readonly Dictionary<(string Exchange, MarketType MarketType), IExchangeMarketDataCollector> _collectors;

    public MarketDataCollectorResolver(IEnumerable<IExchangeMarketDataCollector> collectors)
    {
        _collectors = collectors.ToDictionary(
            c => (c.Exchange.ToLowerInvariant(), c.MarketType),
            c => c);
    }

    public IExchangeMarketDataCollector Resolve(string exchange, MarketType marketType)
    {
        var key = (exchange.Trim().ToLowerInvariant(), marketType);
        if (_collectors.TryGetValue(key, out var collector))
            return collector;

        throw new ArgumentException(
            $"Unsupported exchange/marketType: {exchange}/{marketType}. Allowed exchanges: binance, okx. Allowed marketType: spot.");
    }
}
