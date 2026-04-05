namespace NetGding.Models.Indicators.Volume
{
    public class Volume
    {
        public static readonly List<int> Periods = new List<int> { 20 };
        public Dictionary<string, float> Values { get; set; } = new Dictionary<string, float>();
    }
}