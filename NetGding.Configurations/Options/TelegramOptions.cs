namespace NetGding.Configurations.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";
    public int PollingTimeoutSeconds { get; set; } = 30;
    public string WebApiBaseUrl { get; set; } = "http://localhost:5001";
    public int NotifierHttpTimeoutSeconds { get; set; } = 60;
    public int WebApiHttpTimeoutSeconds { get; set; } = 90;
    public int PollingErrorRetrySeconds { get; set; } = 5;
    public int OnDemandMaxRetries { get; set; } = 3;
    public int OnDemandRetryBaseDelaySeconds { get; set; } = 2;
}