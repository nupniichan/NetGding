namespace NetGding.Analyzer.Signal;

public sealed class SignalEngineOptions
{
    public const string SectionName = "SignalEngine";

    public float MinConfidence { get; set; }
    public float TradeConfidence { get; set; }
    public float ReversalConfidence { get; set; }
    public double AtrSlMultiplier { get; set; }
    public double AtrTpMultiplier { get; set; }
    public string FastEmaPeriod { get; set; } = "";
    public string SlowEmaPeriod { get; set; } = "";
}