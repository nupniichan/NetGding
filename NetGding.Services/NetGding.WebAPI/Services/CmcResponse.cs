using System.Text.Json.Serialization;

namespace NetGding.WebApi.Services;

internal sealed class CmcResponse
{
    [JsonPropertyName("data")]
    public CmcData? Data { get; set; }
}
