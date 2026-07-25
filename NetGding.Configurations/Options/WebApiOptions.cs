namespace NetGding.Configurations.Options;

public sealed class WebApiOptions
{
    public const string SectionName = "WebApi";

    public string TelegramServiceUrl { get; set; } = "";
    public string DiscordServiceUrl { get; set; } = "";
    public string CollectorServiceUrl { get; set; } = "";
    public string AnalyzerServiceUrl { get; set; } = "";
    public string NewsServiceUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; }
    public int CollectorTimeoutSeconds { get; set; }
    public int HealthTimeoutSeconds { get; set; }
    public string HealthProbePath { get; set; } = "";
    public int AnalysisHistoryLimit { get; set; }
    public int MaxRetries { get; set; }
    public int NewsDefaultLimit { get; set; }
    public int NewsMaxLimit { get; set; }
    public string[] Symbols { get; set; } = [];
    public string[] BarTimeFrames { get; set; } = [];
    public string OutputDirectory { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public string AlphaVantageApiKey { get; set; } = "";
    public string CoinMarketCapApiKey { get; set; } = "";
    public int NewsCacheRefreshHours { get; set; } = 6;
    public int NewsRetentionDays { get; set; } = 5;
}