using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class GoogleNewsRssNewsProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleNewsRssNewsProvider> _logger;

    public GoogleNewsRssNewsProvider(
        HttpClient httpClient,
        ILogger<GoogleNewsRssNewsProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var searchQuery = BuildSearchQuery(symbol);
        var url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(searchQuery)}&hl=en-US&gl=US&ceid=US:en";

        try
        {
            var xmlContent = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
            var doc = XDocument.Parse(xmlContent);

            var items = doc.Root?.Element("channel")?.Elements("item");
            if (items is null)
                return [];

            var results = new List<NewsItemDto>();
            foreach (var item in items)
            {
                var rawTitle = item.Element("title")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(rawTitle))
                    continue;

                var source = item.Element("source")?.Value ?? "";
                var title = rawTitle;

                if (string.IsNullOrWhiteSpace(source) && rawTitle.Contains(" - "))
                {
                    var lastDash = rawTitle.LastIndexOf(" - ", StringComparison.Ordinal);
                    source = rawTitle[(lastDash + 3)..].Trim();
                    title = rawTitle[..lastDash].Trim();
                }
                else if (!string.IsNullOrWhiteSpace(source) && title.EndsWith($" - {source}", StringComparison.OrdinalIgnoreCase))
                {
                    title = title[..^(source.Length + 3)].Trim();
                }

                if (string.IsNullOrWhiteSpace(source))
                    source = "Google News";

                var link = item.Element("link")?.Value ?? "";
                var pubDateStr = item.Element("pubDate")?.Value ?? "";
                var publishedAt = ParsePubDate(pubDateStr);

                if (fromUtc.HasValue && publishedAt < fromUtc.Value)
                    continue;
                if (toUtc.HasValue && publishedAt > toUtc.Value)
                    continue;

                var descriptionHtml = item.Element("description")?.Value ?? "";
                var summary = CleanHtml(descriptionHtml);
                if (string.IsNullOrWhiteSpace(summary) || summary == title)
                    summary = title;

                var id = GenerateIdFromUrl(link);

                results.Add(new NewsItemDto(
                    id,
                    symbol,
                    title,
                    source,
                    link,
                    publishedAt,
                    summary,
                    "neutral"));

                if (results.Count >= limit)
                    break;
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GoogleNewsRssNewsProvider: Failed to fetch RSS news for {Symbol}", symbol);
            return [];
        }
    }

    private static string BuildSearchQuery(string symbol)
    {
        var cleaned = symbol.Trim().ToUpperInvariant();
        if (cleaned.Contains('/'))
            cleaned = cleaned.Split('/')[0];
        else if (cleaned.Contains('-'))
            cleaned = cleaned.Split('-')[0];
        else if (cleaned.EndsWith("USDT") && cleaned.Length > 4)
            cleaned = cleaned[..^4];
        else if (cleaned.EndsWith("USD") && cleaned.Length > 3)
            cleaned = cleaned[..^3];

        return cleaned switch
        {
            "BTC" => "Bitcoin crypto",
            "ETH" => "Ethereum crypto",
            "SOL" => "Solana crypto",
            "XRP" => "Ripple XRP crypto",
            "BNB" => "Binance BNB crypto",
            "DOGE" => "Dogecoin crypto",
            "ADA" => "Cardano crypto",
            "AVAX" => "Avalanche crypto",
            _ => $"{cleaned} crypto news"
        };
    }

    private static DateTime ParsePubDate(string pubDate)
    {
        if (DateTimeOffset.TryParse(pubDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            return dto.UtcDateTime;
        }
        return DateTime.UtcNow;
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var plain = Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(plain).Trim();
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
