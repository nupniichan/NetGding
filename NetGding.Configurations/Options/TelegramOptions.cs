namespace NetGding.Configurations.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public int PollingTimeoutSeconds { get; set; }
    public string WebApiBaseUrl { get; set; } = "";
    public int NotifierHttpTimeoutSeconds { get; set; }
    public int WebApiHttpTimeoutSeconds { get; set; }
    public int PollingErrorRetrySeconds { get; set; }
    public int OnDemandMaxRetries { get; set; }
    public int OnDemandRetryBaseDelaySeconds { get; set; }
}