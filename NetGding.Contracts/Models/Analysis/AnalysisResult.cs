using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Contracts.Models.Analysis;

public sealed class AnalysisResult
{
    public required string Symbol { get; set; }
    public AssetMarket Market { get; set; }
    public MarketType MarketType { get; set; }
    public required string Timeframe { get; set; }
    public decimal CurrentPrice { get; set; }
    public IndicatorSnapshot Indicators { get; set; } = new();
    public MarketStructure MarketStructure { get; set; } = new();
    public TradeDecision Decision { get; set; }
    public string Reason { get; set; } = "";
    public string ExpectedHoldTime { get; set; } = "";
    public RiskManagement RiskManagement { get; set; } = new();
    public DateTime AnalyzedAtUtc { get; set; }
}