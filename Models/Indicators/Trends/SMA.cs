namespace NetGding.Models.Indicators
{
    // Simple Moving Average
    public class SMA
    {
        public static readonly List<int> Periods = new List<int>() { 20, 50, 200 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}