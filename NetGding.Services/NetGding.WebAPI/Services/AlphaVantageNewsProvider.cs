using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Exceptions;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class AlphaVantageNewsProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<AlphaVantageNewsProvider> _logger;

    public AlphaVantageNewsProvider(
        HttpClient httpClient,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<AlphaVantageNewsProvider> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        var apiKey = o.AlphaVantageApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("AlphaVantageNewsProvider: AlphaVantageApiKey is not configured, returning empty news.");
            return [];
        }

        var ticker = NormalizeTickerForAlphaVantage(symbol);
        var url = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers={ticker}&apikey={apiKey}&limit={limit}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<AlphaVantageNewsResponse>(url, ct)
                .ConfigureAwait(false);

            if (response?.Feed is null || response.Feed.Count == 0)
            {
                if (response is not null)
                {
                    if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
                    {
                        throw new NetGding.Contracts.Exceptions.NetGdingException(
                            ErrorCodes.AlphaVantageApiError,
                            "AlphaVantageNewsProvider.GetNewsAsync",
                            $"AlphaVantage API error: {response.ErrorMessage}");
                    }
                    if (!string.IsNullOrWhiteSpace(response.Note))
                    {
                        throw new NetGding.Contracts.Exceptions.NetGdingException(
                            ErrorCodes.AlphaVantageRateLimit,
                            "AlphaVantageNewsProvider.GetNewsAsync",
                            $"AlphaVantage API rate limit: {response.Note}");
                    }
                    if (!string.IsNullOrWhiteSpace(response.Information))
                    {
                        throw new NetGding.Contracts.Exceptions.NetGdingException(
                            ErrorCodes.AlphaVantageRateLimit,
                            "AlphaVantageNewsProvider.GetNewsAsync",
                            $"AlphaVantage API info/rate limit: {response.Information}");
                    }
                }
                return [];
            }

            var results = new List<NewsItemDto>();
            foreach (var item in response.Feed)
            {
                var publishedAt = ParseTimePublished(item.TimePublished);
                if (fromUtc.HasValue && publishedAt < fromUtc.Value)
                    continue;
                if (toUtc.HasValue && publishedAt > toUtc.Value)
                    continue;

                results.Add(new NewsItemDto(
                    GenerateIdFromUrl(item.Url),
                    symbol,
                    item.Title,
                    item.Source,
                    item.Url,
                    publishedAt,
                    item.Summary,
                    item.OverallSentimentLabel));

                if (results.Count >= limit)
                    break;
            }

            return results;
        }
        catch (Exception ex) when (ex is not NetGding.Contracts.Exceptions.NetGdingException)
        {
            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.AlphaVantageApiFailed,
                "AlphaVantageNewsProvider.GetNewsAsync",
                $"AlphaVantage API call failed for ticker '{ticker}': {ex.Message}", ex);
        }
    }

    private static string NormalizeTickerForAlphaVantage(string symbol)
    {
        var cleaned = symbol.Trim().ToUpperInvariant();
        if (cleaned.StartsWith("CRYPTO:", StringComparison.OrdinalIgnoreCase))
            return cleaned;

        string coin = cleaned;
        if (cleaned.Contains('/'))
            coin = cleaned.Split('/')[0];
        else if (cleaned.Contains('-'))
            coin = cleaned.Split('-')[0];
        else if (cleaned.EndsWith("USDT") && cleaned.Length > 4)
            coin = cleaned[..^4];
        else if (cleaned.EndsWith("USD") && cleaned.Length > 3)
            coin = cleaned[..^3];

        return $"CRYPTO:{coin}";
    }

    private static DateTime ParseTimePublished(string timePublished)
    {
        if (string.IsNullOrWhiteSpace(timePublished))
            return DateTime.UtcNow;

        return DateTime.TryParseExact(timePublished, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : DateTime.UtcNow;
    }

    private static long GenerateIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;
        ulong hash = 14695981039346656037UL;
        foreach (char c in url)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return (long)hash;
    }
}
