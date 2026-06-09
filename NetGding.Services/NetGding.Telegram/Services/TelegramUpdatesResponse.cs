using System.Text.Json.Serialization;

namespace NetGding.Telegram.Services;

public sealed record TelegramUpdatesResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] TelegramUpdate[]? Result);
