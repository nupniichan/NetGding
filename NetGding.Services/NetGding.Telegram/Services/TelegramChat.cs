using System.Text.Json.Serialization;

namespace NetGding.Telegram.Services;

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id);
