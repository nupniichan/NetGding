using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class CoinMarketCapFearAndGreedProvider : IFearAndGreedProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<CoinMarketCapFearAndGreedProvider> _logger;

    public CoinMarketCapFearAndGreedProvider(
        HttpClient httpClient,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<CoinMarketCapFearAndGreedProvider> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<FearAndGreedDto?> GetLatestAsync(CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        var cmcApiKey = o.CoinMarketCapApiKey;

        if (!string.IsNullOrWhiteSpace(cmcApiKey))
        {
            try
            {
                _logger.LogInformation("CoinMarketCapFearAndGreedProvider: Fetching sentiment from CoinMarketCap API");
                var request = new HttpRequestMessage(HttpMethod.Get, "https://pro-api.coinmarketcap.com/v3/fear-and-greed/latest");
                request.Headers.TryAddWithoutValidation("X-CMC_PRO_API_KEY", cmcApiKey);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadFromJsonAsync<CmcResponse>(cancellationToken: ct).ConfigureAwait(false);
                if (content?.Data is not null)
                {
                    var val = ParseValue(content.Data.Value);
                    var classification = content.Data.ValueClassification ?? "Neutral";
                    var timestamp = ParseTimestamp(content.Data.Timestamp);

                    _logger.LogInformation("CoinMarketCapFearAndGreedProvider: CMC returned value={Value} ({Classification})", val, classification);
                    return new FearAndGreedDto(val, classification, timestamp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoinMarketCapFearAndGreedProvider: Failed to fetch from CoinMarketCap");
            }
        }
        else
        {
            _logger.LogWarning("CoinMarketCapFearAndGreedProvider: CoinMarketCapApiKey is empty.");
        }

        return null;
    }

    private static int ParseValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intVal))
            return intVal;
        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsedInt))
            return parsedInt;
        return 50;
    }

    private static DateTime ParseTimestamp(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unixSeconds))
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (long.TryParse(str, out var unixSecs))
                return DateTimeOffset.FromUnixTimeSeconds(unixSecs).UtcDateTime;
            if (DateTime.TryParse(str, out var parsedDt))
                return parsedDt;
        }
        return DateTime.UtcNow;
    }

    private sealed class CmcResponse
    {
        [JsonPropertyName("data")]
        public CmcData? Data { get; set; }
    }

    private sealed class CmcData
    {
        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }

        [JsonPropertyName("value_classification")]
        public string? ValueClassification { get; set; }

        [JsonPropertyName("timestamp")]
        public JsonElement Timestamp { get; set; }
    }
}
