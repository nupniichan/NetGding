namespace NetGding.Models.Indicators.Momentum
{
    public class Stochastic
    {
        public static readonly List<int> Periods = new List<int> { 14, 3, 3 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}