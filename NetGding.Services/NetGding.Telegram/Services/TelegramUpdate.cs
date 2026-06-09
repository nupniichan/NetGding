using System.Text.Json.Serialization;

namespace NetGding.Telegram.Services;

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);
