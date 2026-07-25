namespace NetGding.Contracts.Models.News;

public sealed record NewsItem(
    string Title,
    string Summary,
    string Url,
    string Source,
    string? Sentiment = null);

public sealed record NewsResponse(
    string Symbol,
    int Count,
    IReadOnlyList<NewsItem> Items);
