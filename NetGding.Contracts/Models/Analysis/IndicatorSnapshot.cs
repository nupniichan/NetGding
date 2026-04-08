namespace NetGding.Contracts.Models.Analysis;

public sealed class IndicatorSnapshot
{
    public Dictionary<string, float> Ema { get; set; } = new();
    public Dictionary<string, float> Macd { get; set; } = new();
    public Dictionary<string, float> Rsi { get; set; } = new();
    public Dictionary<string, float> BollingerBands { get; set; } = new();
    public Dictionary<string, float> Atr { get; set; } = new();
    public Dictionary<string, float> VolumeMa { get; set; } = new();
    public Dictionary<string, float> Vwap { get; set; } = new();
}