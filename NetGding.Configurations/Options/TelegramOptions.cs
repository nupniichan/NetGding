namespace NetGding.Configurations.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public int PollingTimeoutSeconds { get; set; } = 30;
    public string CollectorBaseUrl { get; set; } = "http://localhost:5000";
}
