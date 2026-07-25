namespace NetGding.WebApi.Persistence;

public sealed class NewsItemEntity
{
    public long Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Sentiment { get; set; }
    public DateTime FetchedAtUtc { get; set; }
}
