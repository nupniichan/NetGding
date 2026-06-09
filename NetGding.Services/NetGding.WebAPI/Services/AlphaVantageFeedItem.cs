using System.Text.Json.Serialization;

namespace NetGding.WebApi.Services;

internal sealed class AlphaVantageFeedItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("time_published")]
    public string TimePublished { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("overall_sentiment_label")]
    public string OverallSentimentLabel { get; set; } = "";
}
