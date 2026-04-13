namespace NetGding.Analyzer.Signal;

public sealed class SignalEngineOptions
{
    public const string SectionName = "SignalEngine";

    public float MinConfidence { get; set; } = 0.6f;
    public float TradeConfidence { get; set; } = 0.65f;
    public float ReversalConfidence { get; set; } = 0.8f;
    public double AtrSlMultiplier { get; set; } = 1.5;
    public double AtrTpMultiplier { get; set; } = 2.0;
    public string FastEmaPeriod { get; set; } = "9";
    public string SlowEmaPeriod { get; set; } = "21";
}