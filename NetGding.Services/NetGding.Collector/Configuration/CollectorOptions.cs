namespace NetGding.Collector.Configuration;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public bool UsePaper { get; set; } = true;
    public string Symbol { get; set; } = "AAPL";
    public string BarTimeFrame { get; set; } = "1h";
    public int LookbackDays { get; set; } = 5;
    public int PollIntervalSeconds { get; set; } = 3600;
}