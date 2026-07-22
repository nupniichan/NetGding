namespace NetGding.Contracts.Models.Indicators.Volume;

public sealed class VWAP
{
    public Dictionary<string, float> Values { get; set; } = new();
}