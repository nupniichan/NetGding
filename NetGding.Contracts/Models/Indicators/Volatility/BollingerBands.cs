namespace NetGding.Contracts.Models.Indicators.Volatility
{
    public class BollingerBands
    {
        public static readonly List<int> Periods = new List<int> { 20 };
        public static readonly float StandardDeviationMultiplier = 2f;
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}