using System.Globalization;
using System.Text.Json;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

public sealed class BinanceMarketDataCollector : IExchangeMarketDataCollector
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BinanceMarketDataCollector> _logger;

    public BinanceMarketDataCollector(
        IHttpClientFactory httpClientFactory,
        ILogger<BinanceMarketDataCollector> logger,
        MarketType marketType)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        MarketType = marketType;
    }

    public string Exchange => "binance";
    public MarketType MarketType { get; }

    public async Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        string timeframe,
        CancellationToken ct = default)
    {
        var normalized = NormalizeBinanceSymbol(symbol);
        var interval = ToBinanceInterval(timeframe);
        var startMs = new DateTimeOffset(fromUtc).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(toUtc).ToUnixTimeMilliseconds();

        var baseUrl = MarketType == MarketType.Future
            ? "https://fapi.binance.com/fapi/v1/klines"
            : "https://api.binance.com/api/v3/klines";
        var url = $"{baseUrl}?symbol={normalized}&interval={interval}&startTime={startMs}&endTime={endMs}&limit=1000";

        using var response = await _httpClientFactory.CreateClient(nameof(BinanceMarketDataCollector))
            .GetAsync(url, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var bars = new List<OhlcvBar>();

        foreach (var row in json.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
                continue;

            var ts = DateTimeOffset.FromUnixTimeMilliseconds(row[0].GetInt64()).UtcDateTime;
            var open = ParseInvariant(row[1].GetString());
            var high = ParseInvariant(row[2].GetString());
            var low = ParseInvariant(row[3].GetString());
            var close = ParseInvariant(row[4].GetString());
            var volume = ParseInvariant(row[5].GetString());
            bars.Add(new OhlcvBar(ts, open, high, low, close, volume));
        }

        bars.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));
        _logger.LogDebug("Binance collector [{MarketType}]: {Symbol} -> {Count} bars", MarketType, normalized, bars.Count);
        return bars;
    }

    private static string ToBinanceInterval(string timeframe) => timeframe.Trim().ToLowerInvariant() switch
    {
        "15m" => "15m",
        "1h" => "1h",
        "4h" => "4h",
        "1d" => "1d",
        "1w" => "1w",
        "1m" => "1M",
        _ => throw new ArgumentException($"Unsupported timeframe '{timeframe}'.", nameof(timeframe))
    };

    private static string NormalizeBinanceSymbol(string symbol)
    {
        var cleaned = symbol.Trim().ToUpperInvariant().Replace("-", "/");
        if (cleaned.Contains('/'))
        {
            var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
                return $"{parts[0]}{parts[1]}";
        }

        if (cleaned.EndsWith("USDT", StringComparison.Ordinal))
            return cleaned;

        if (cleaned.EndsWith("USD", StringComparison.Ordinal))
            return cleaned[..^3] + "USDT";

        return cleaned + "USDT";
    }

    private static double ParseInvariant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0d;
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
