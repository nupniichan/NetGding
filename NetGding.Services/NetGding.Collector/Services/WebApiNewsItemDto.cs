using System;

namespace NetGding.Collector.Services;

public sealed record WebApiNewsItemDto(
    long Id,
    string Symbol,
    string Title,
    string Source,
    string Url,
    DateTime PublishedAtUtc,
    string Summary,
    string? Sentiment);
