using System.Collections.Generic;

namespace NetGding.Telegram.Services;

public sealed record TelegramNewsResponse(
    string Symbol,
    int Count,
    IReadOnlyList<TelegramNewsItem> Items);
