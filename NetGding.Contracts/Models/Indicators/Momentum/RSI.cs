namespace NetGding.Models.Indicators.Momentum
{
    // Relative Strength Index
    public class RSI
    {
        public static readonly List<int> Periods = new List<int> { 14 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}