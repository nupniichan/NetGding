namespace NetGding.Contracts.Models.Analysis;

public sealed class RiskManagement
{
    public FuturesRisk? Futures { get; set; }
    public SpotRisk? Spot { get; set; }
}