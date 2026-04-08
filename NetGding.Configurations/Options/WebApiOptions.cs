namespace NetGding.Configurations.Options;

public sealed class WebApiOptions
{
    public const string SectionName = "WebApi";

    public string TelegramServiceUrl { get; set; } = "http://localhost:5002";
    public string CollectorServiceUrl { get; set; } = "http://localhost:5000";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
