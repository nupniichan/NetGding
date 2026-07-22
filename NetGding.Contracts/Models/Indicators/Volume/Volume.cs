namespace NetGding.Contracts.Models.Indicators.Volume;

public sealed class Volume
{
    public static readonly IReadOnlyList<int> Periods = [20];
    public Dictionary<string, float> Values { get; set; } = new();
}