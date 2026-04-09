namespace NetGding.WebApi.Models;

public sealed record NewsItemDto(
    long Id,
    string Symbol,
    string Title,
    string Source,
    string Url,
    DateTime PublishedAtUtc,
    string Summary,
    string? Sentiment = null);
