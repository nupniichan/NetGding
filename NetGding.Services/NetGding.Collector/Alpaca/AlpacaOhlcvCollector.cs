using Alpaca.Markets;
using Microsoft.Extensions.Logging;
using NetGding.Models.MarketData;

namespace NetGding.Collector.Alpaca;

public sealed class AlpacaOhlcvCollector : IAlpacaOhlcvCollector
{
    private readonly IAlpacaDataClient _client;
    private readonly ILogger<AlpacaOhlcvCollector> _logger;

    public AlpacaOhlcvCollector(IAlpacaDataClient client, ILogger<AlpacaOhlcvCollector> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        BarTimeFrame timeFrame,
        CancellationToken cancellationToken = default)
    {
        var request = new HistoricalBarsRequest(symbol, fromUtc, toUtc, timeFrame);
        var result = await _client.GetHistoricalBarsAsync(request, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IBar>? bars = null;
        if (result.Items.TryGetValue(symbol, out var byKey))
            bars = byKey;
        else if (result.Items.TryGetValue(symbol.ToUpperInvariant(), out var byUpper))
            bars = byUpper;
        else if (result.Items.Count == 1)
            bars = result.Items.Values.First();

        if (bars == null || bars.Count == 0)
        {
            _logger.LogDebug("No bars returned for {Symbol}", symbol);
            return Array.Empty<OhlcvBar>();
        }

        var list = new List<OhlcvBar>(bars.Count);
        foreach (var b in bars)
            list.Add(OhlcvBarMapper.FromAlpaca(b));
        return list;
    }
}