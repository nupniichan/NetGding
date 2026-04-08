using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Contracts.Models.Analysis;

public sealed class MarketStructure
{
    public TrendDirection ShortTermTrend { get; set; }
    public TrendDirection MidTermTrend { get; set; }
    public TrendDirection LongTermTrend { get; set; }
}
