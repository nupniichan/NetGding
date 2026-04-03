
namespace NetGding.Models.Indicators.Trends
{
    // Moving Average Convergence Divergence
    public class MACD
    {
        public static readonly List<int> Periods = new List<int> { 12, 26, 9};
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}
