namespace NetGding.Contracts.Models.Indicators.Volatility;

// Average True Range
public sealed class ATR
{
    public static readonly IReadOnlyList<int> Periods = [14];
    public Dictionary<string, float> Values { get; set; } = new();
}