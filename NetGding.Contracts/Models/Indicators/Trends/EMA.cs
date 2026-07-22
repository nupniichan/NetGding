namespace NetGding.Contracts.Models.Indicators.Trends;

// Exponential Moving Average
public sealed class EMA
{
    public static readonly IReadOnlyList<int> Periods = [9, 21, 34, 50, 89, 100, 200];
    public Dictionary<string, float> Values { get; set; } = new();
}