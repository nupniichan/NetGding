namespace NetGding.Contracts.Models.Indicators.Trends;

// Moving Average Convergence Divergence
public sealed class MACD
{
    public static readonly IReadOnlyList<int> Periods = [12, 26, 9];
    public Dictionary<string, float> Values { get; set; } = new();
}
