namespace NetGding.Contracts.Models.Indicators.Momentum;

// Relative Strength Index
public sealed class RSI
{
    public static readonly IReadOnlyList<int> Periods = [14];
    public Dictionary<string, float> Values { get; set; } = new();
}