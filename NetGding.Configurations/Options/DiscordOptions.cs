namespace NetGding.Configurations.Options;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string BotToken { get; set; } = "";
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public string WebApiBaseUrl { get; set; } = "";
    public int WebApiHttpTimeoutSeconds { get; set; }
    public int OnDemandMaxRetries { get; set; }
    public int OnDemandRetryBaseDelaySeconds { get; set; }
}