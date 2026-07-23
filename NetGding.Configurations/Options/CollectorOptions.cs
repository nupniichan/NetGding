namespace NetGding.Configurations.Options;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public int LookbackDays { get; set; }
    public string OutputDirectory { get; set; } = "";
    public bool WebApiPublishEnabled { get; set; }
    public string WebApiBaseUrl { get; set; } = "";
    public int WebApiHttpTimeoutSeconds { get; set; }
    public int PublishMaxRetries { get; set; }
    public bool ChartEnabled { get; set; }
    public string ChartImgApiKey { get; set; } = "";
}