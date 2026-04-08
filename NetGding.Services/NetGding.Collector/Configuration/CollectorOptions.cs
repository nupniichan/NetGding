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

    // News settings
    public bool NewsEnabled { get; set; } = true;
    public int NewsPollingIntervalMinutes { get; set; } = 5;
    public int NewsLookbackHours { get; set; } = 24;

    // AI Analysis settings
    public bool AnalysisEnabled { get; set; } = true;
    public string GemmaBaseUrl { get; set; } = ";
    public string GemmaApiKey { get; set; } = "";
    public string GemmaModel { get; set; } = "";
}