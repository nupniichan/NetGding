namespace NetGding.Models.Indicators.SupportResistance
{
    public class FibonacciRetracement
    {
        public static readonly List<float> Levels = new List<float> { 0.236f, 0.382f, 0.5f, 0.618f, 0.786f };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}