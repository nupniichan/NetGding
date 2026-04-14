using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Signal;

public interface IRiskCalculator
{
    RiskManagement Calculate(
        TradeDecision decision,
        decimal currentPrice,
        IndicatorSnapshot indicators,
        MarketType marketType);
}