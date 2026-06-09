using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetGding.WebApi.Services;

internal sealed class AlphaVantageNewsResponse
{
    [JsonPropertyName("feed")]
    public List<AlphaVantageFeedItem>? Feed { get; set; }

    [JsonPropertyName("Note")]
    public string? Note { get; set; }

    [JsonPropertyName("Information")]
    public string? Information { get; set; }

    [JsonPropertyName("Error Message")]
    public string? ErrorMessage { get; set; }
}
