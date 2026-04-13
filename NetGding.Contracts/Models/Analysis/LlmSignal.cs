using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Contracts.Models.Analysis;

public sealed class LlmSignal
{
    public TrendBias Trend { get; set; }
    public MomentumState Momentum { get; set; }
    public VolatilityLevel Volatility { get; set; }
    public float Confidence { get; set; }
    public string Reason { get; set; } = "";
    public float NewsImpact { get; set; }
}