using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetGding.WebApi.Services;

internal sealed class CmcData
{
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("value_classification")]
    public string? ValueClassification { get; set; }

    [JsonPropertyName("timestamp")]
    public JsonElement Timestamp { get; set; }
}
