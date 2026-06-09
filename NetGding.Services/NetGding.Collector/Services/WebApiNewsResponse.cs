namespace NetGding.Collector.Services;

public sealed record WebApiNewsResponse(
    string Symbol,
    int Count,
    IReadOnlyList<WebApiNewsItemDto> Items);
