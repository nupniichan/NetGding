namespace NetGding.Configurations.Options;

public sealed class WebApiOptions
{
    public const string SectionName = "WebApi";

    public string TelegramServiceUrl { get; set; } = "http://localhost:5002";
    public string DiscordServiceUrl { get; set; } = "http://localhost:5003";
    public string CollectorServiceUrl { get; set; } = "http://localhost:5000";
    public string AnalyzerServiceUrl { get; set; } = "";
    public string NewsServiceUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int HealthTimeoutSeconds { get; set; } = 5;
    public string HealthProbePath { get; set; } = "/health";
    public int AnalysisHistoryLimit { get; set; } = 500;
    public int MaxRetries { get; set; } = 3;
    public int NewsDefaultLimit { get; set; } = 20;
    public int NewsMaxLimit { get; set; } = 200;
    public string[] Symbols { get; set; } = [];
    public string[] BarTimeFrames { get; set; } = [];
    public string OutputDirectory { get; set; } = "";
}