using System.Globalization;
using System.Text.Json;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

public sealed class OkxMarketDataCollector : IExchangeMarketDataCollector
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OkxMarketDataCollector> _logger;

    public OkxMarketDataCollector(
        IHttpClientFactory httpClientFactory,
        ILogger<OkxMarketDataCollector> logger,
        MarketType marketType)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        MarketType = marketType;
    }

    public string Exchange => "okx";
    public MarketType MarketType { get; }

    public async Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        string timeframe,
        CancellationToken ct = default)
    {
        _ = fromUtc;
        _ = toUtc;

        var instId = NormalizeOkxInstrumentId(symbol, MarketType);
        var bar = ToOkxBar(timeframe);
        var url = $"https://www.okx.com/api/v5/market/candles?instId={instId}&bar={bar}&limit=300";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-simulated-trading", "0");

        using var response = await _httpClientFactory.CreateClient(nameof(OkxMarketDataCollector))
            .SendAsync(request, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogError("OKX collector [{MarketType}]: API request failed for {Instrument} ({StatusCode}): {Body}",
                MarketType, instId, response.StatusCode, errorBody);

            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.MarketDataFetchFailed,
                "OkxMarketDataCollector.CollectAsync",
                $"OKX API request failed for {instId} ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var bars = new List<OhlcvBar>();
        if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in data.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 6)
                    continue;

                var ts = DateTimeOffset.FromUnixTimeMilliseconds(ParseLong(row[0].GetString())).UtcDateTime;
                var open = ParseInvariant(row[1].GetString());
                var high = ParseInvariant(row[2].GetString());
                var low = ParseInvariant(row[3].GetString());
                var close = ParseInvariant(row[4].GetString());
                var volume = ParseInvariant(row[5].GetString());
                bars.Add(new OhlcvBar(ts, open, high, low, close, volume));
            }
        }

        bars.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));
        _logger.LogDebug("OKX collector [{MarketType}]: {Instrument} -> {Count} bars", MarketType, instId, bars.Count);
        return bars;
    }

    private static string ToOkxBar(string timeframe) => timeframe.Trim().ToLowerInvariant() switch
    {
        "15m" => "15m",
        "1h" => "1H",
        "4h" => "4H",
        "1d" => "1D",
        "1w" => "1W",
        "1m" => "1M",
        _ => throw new ArgumentException($"Unsupported timeframe '{timeframe}'.", nameof(timeframe))
    };

    private static string NormalizeOkxInstrumentId(string symbol, MarketType marketType)
    {
        var cleaned = symbol.Trim().ToUpperInvariant().Replace("/", "-");
        var baseInstrument = cleaned.Contains('-', StringComparison.Ordinal)
            ? cleaned
            : cleaned.EndsWith("USDT", StringComparison.Ordinal)
                ? cleaned.Replace("USDT", "-USDT", StringComparison.Ordinal)
                : cleaned.EndsWith("USD", StringComparison.Ordinal)
                    ? cleaned.Replace("USD", "-USDT", StringComparison.Ordinal)
                    : $"{cleaned}-USDT";

        if (marketType == MarketType.Future && !baseInstrument.EndsWith("-SWAP", StringComparison.Ordinal))
            return $"{baseInstrument}-SWAP";

        if (marketType == MarketType.Spot && baseInstrument.EndsWith("-SWAP", StringComparison.Ordinal))
            return baseInstrument[..^5];

        return baseInstrument;
    }

    private static long ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0L;
        return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public async Task<MarketDepthDto?> GetDepthAsync(
        string symbol,
        int limit = 10,
        CancellationToken ct = default)
    {
        var instId = NormalizeOkxInstrumentId(symbol, MarketType);
        var url = $"https://www.okx.com/api/v5/market/books?instId={instId}&sz={limit}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-simulated-trading", "0");

            using var response = await _httpClientFactory.CreateClient(nameof(OkxMarketDataCollector))
                .SendAsync(request, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OKX GetDepthAsync failed for {Symbol}: {StatusCode}", symbol, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var bids = new List<DepthEntryDto>();
            var asks = new List<DepthEntryDto>();

            if (json.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array && dataProp.GetArrayLength() > 0)
            {
                var first = dataProp[0];
                if (first.TryGetProperty("bids", out var bidsProp) && bidsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in bidsProp.EnumerateArray())
                    {
                        if (entry.ValueKind == JsonValueKind.Array && entry.GetArrayLength() >= 2)
                        {
                            var price = ParseInvariant(entry[0].GetString());
                            var qty = ParseInvariant(entry[1].GetString());
                            bids.Add(new DepthEntryDto(price, qty));
                        }
                    }
                }

                if (first.TryGetProperty("asks", out var asksProp) && asksProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in asksProp.EnumerateArray())
                    {
                        if (entry.ValueKind == JsonValueKind.Array && entry.GetArrayLength() >= 2)
                        {
                            var price = ParseInvariant(entry[0].GetString());
                            var qty = ParseInvariant(entry[1].GetString());
                            asks.Add(new DepthEntryDto(price, qty));
                        }
                    }
                }
            }

            double spread = 0;
            double spreadPercentage = 0;
            if (asks.Count > 0 && bids.Count > 0)
            {
                spread = asks[0].Price - bids[0].Price;
                spreadPercentage = (spread / bids[0].Price) * 100.0;
            }

            return new MarketDepthDto(symbol, Exchange, bids, asks, spread, spreadPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OKX GetDepthAsync exception for {Symbol}", symbol);
            return null;
        }
    }

    private static double ParseInvariant(string? value) =>
        MarketParsingHelper.ParseInvariantDouble(value);
}

