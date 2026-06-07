namespace NetGding.Configurations.Options;

public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    public int LookbackDays { get; set; } = 30;
    public string OutputDirectory { get; set; } = "";
    public bool WebApiPublishEnabled { get; set; }
    public string WebApiBaseUrl { get; set; } = "http://localhost:5001";
    public int WebApiHttpTimeoutSeconds { get; set; } = 10;
    public int PublishMaxRetries { get; set; } = 3;
    public bool ChartEnabled { get; set; } = true;
    public string ChartImgApiKey { get; set; } = "";
}