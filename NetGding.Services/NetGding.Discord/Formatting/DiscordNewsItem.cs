using System;

namespace NetGding.Discord.Formatting;

public sealed record DiscordNewsItem(
    long Id,
    string Symbol,
    string Title,
    string Source,
    string Url,
    DateTime PublishedAtUtc,
    string Summary,
    string? Sentiment = null);
