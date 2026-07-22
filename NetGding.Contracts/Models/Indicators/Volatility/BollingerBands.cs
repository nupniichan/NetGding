namespace NetGding.Contracts.Models.Indicators.Volatility;

public sealed class BollingerBands
{
    public static readonly IReadOnlyList<int> Periods = [20];
    public const float StandardDeviationMultiplier = 2f;
    public Dictionary<string, float> Values { get; set; } = new();
}