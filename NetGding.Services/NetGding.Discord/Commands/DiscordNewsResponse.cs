using System.Collections.Generic;
using NetGding.Discord.Formatting;

namespace NetGding.Discord.Commands;

public sealed record DiscordNewsResponse(
    string Symbol,
    int Count,
    IReadOnlyList<DiscordNewsItem> Items);
