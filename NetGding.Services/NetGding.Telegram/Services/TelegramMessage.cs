using System.Text.Json.Serialization;

namespace NetGding.Telegram.Services;

public sealed record TelegramMessage(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("chat")] TelegramChat? Chat);
