using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Signal;

public sealed class RiskCalculator : IRiskCalculator
{
    private readonly SignalEngineOptions _options;

    public RiskCalculator(IOptions<SignalEngineOptions> options)
    {
        _options = options.Value;
    }

    public RiskManagement Calculate(
        TradeDecision decision,
        decimal currentPrice,
        IndicatorSnapshot indicators,
        MarketType marketType)
    {
        if (decision == TradeDecision.Wait)
            return new RiskManagement();

        var atr = GetAtr(indicators);

        return marketType == MarketType.Future
            ? BuildFuturesRisk(decision, currentPrice, atr)
            : BuildSpotRisk(currentPrice, atr);
    }

    private decimal GetAtr(IndicatorSnapshot indicators)
    {
        if (indicators.Atr.Count == 0) return 0m;
        return (decimal)indicators.Atr.Values.Max();
    }

    private RiskManagement BuildFuturesRisk(TradeDecision decision, decimal entry, decimal atr)
    {
        var sl = (decimal)_options.AtrSlMultiplier * atr;
        var tp = (decimal)_options.AtrTpMultiplier * atr;

        return new RiskManagement
        {
            Futures = decision == TradeDecision.Buy
                ? new FuturesRisk
                {
                    Entry = entry,
                    StopLoss = entry - sl,
                    TakeProfit = entry + tp
                }
                : new FuturesRisk
                {
                    Entry = entry,
                    StopLoss = entry + sl,
                    TakeProfit = entry - tp
                }
        };
    }

    private RiskManagement BuildSpotRisk(decimal entry, decimal atr)
    {
        var dcaStep = atr > 0 ? atr : entry * 0.02m;

        return new RiskManagement
        {
            Spot = new SpotRisk
            {
                BuyPrice = entry,
                DcaLevels = [entry - dcaStep, entry - dcaStep * 2]
            }
        };
    }
}