namespace NetGding.Contracts.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public int StreamMaxLen { get; set; } = 10000;
    public int ConsumerPollIntervalMs { get; set; } = 500;
}
