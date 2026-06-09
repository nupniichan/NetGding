using System;

namespace NetGding.Telegram.Services;

public sealed record TelegramNewsItem(
    long Id,
    string Symbol,
    string Title,
    string Source,
    string Url,
    DateTime PublishedAtUtc,
    string Summary,
    string? Sentiment);
