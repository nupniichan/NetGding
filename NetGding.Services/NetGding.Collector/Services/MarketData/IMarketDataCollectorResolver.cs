using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

public interface IMarketDataCollectorResolver
{
    IExchangeMarketDataCollector Resolve(string exchange, MarketType marketType);
}
