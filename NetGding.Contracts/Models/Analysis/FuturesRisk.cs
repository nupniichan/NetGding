namespace NetGding.Contracts.Models.Analysis;

public sealed class FuturesRisk
{
    public decimal? Entry { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
}