namespace NetGding.Configurations.Options;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public bool UsePaper { get; set; } = true;
    public string[] Symbols { get; set; } = [];
    public string[] BarTimeFrames { get; set; } = ["1h"];
    public int LookbackDays { get; set; } = 5;
    public string OutputDirectory { get; set; } = "";
    public bool NewsEnabled { get; set; } = true;
    public int NewsPollingIntervalMinutes { get; set; } = 5;
    public int NewsLookbackHours { get; set; } = 24;
    public bool AnalysisEnabled { get; set; } = true;
    public bool WebApiPublishEnabled { get; set; }
    public string WebApiBaseUrl { get; set; } = "http://localhost:5001";
    public int WebApiHttpTimeoutSeconds { get; set; } = 30;
    public int AutoAnalysisDelaySeconds { get; set; } = 8;
    public int AnalysisCollectionOffsetSeconds { get; set; } = 30;
    public int AnalysisIdlePollMinutes { get; set; } = 1;
    public int PublishMaxRetries { get; set; } = 3;
}