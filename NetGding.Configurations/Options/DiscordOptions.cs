namespace NetGding.Configurations.Options;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string BotToken { get; set; } = "";
    public ulong ChannelId { get; set; } = 0;
    public ulong GuildId { get; set; } = 0;
    public string CollectorBaseUrl { get; set; } = "http://localhost:5000";
    public int CollectorHttpTimeoutSeconds { get; set; } = 180;
    public int OnDemandMaxRetries { get; set; } = 3;
    public int OnDemandRetryBaseDelaySeconds { get; set; } = 2;
}