namespace NetGding.Contracts.Models.Indicators.Volatility
{
    // Average True Range
    public class ATR
    {
        public static readonly List<int> Periods = new List<int> { 14 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}