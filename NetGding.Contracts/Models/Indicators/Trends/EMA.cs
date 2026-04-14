namespace NetGding.Contracts.Models.Indicators.Trends
{
    // Exponential Moving Average
    public class EMA
    {
        public static readonly List<int> Periods = new List<int> { 9, 21, 34, 50, 89, 100, 200 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}