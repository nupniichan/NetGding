namespace NetGding.Contracts.Models.Analysis;

public sealed class SpotRisk
{
    public decimal? BuyPrice { get; set; }
    public IReadOnlyList<decimal> DcaLevels { get; set; } = [];
}