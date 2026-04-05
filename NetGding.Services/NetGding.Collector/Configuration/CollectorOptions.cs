namespace NetGding.Collector.Configuration;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public bool UsePaper { get; set; } = true;
    public string[] Symbols { get; set; } = [];
    public string BarTimeFrame { get; set; } = "1h";
    public int LookbackDays { get; set; } = 5;
    public string OutputDirectory { get; set; } = "";
}