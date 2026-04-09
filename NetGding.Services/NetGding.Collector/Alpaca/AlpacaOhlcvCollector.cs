using Alpaca.Markets;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Alpaca;

public sealed class AlpacaOhlcvCollector : IAlpacaOhlcvCollector
{
    private readonly IAlpacaDataClient _stockClient;
    private readonly IAlpacaCryptoDataClient _cryptoClient;
    private readonly ILogger<AlpacaOhlcvCollector> _logger;

    public AlpacaOhlcvCollector(
        IAlpacaDataClient stockClient,
        IAlpacaCryptoDataClient cryptoClient,
        ILogger<AlpacaOhlcvCollector> logger)
    {
        _stockClient = stockClient;
        _cryptoClient = cryptoClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OhlcvBar>> CollectAsync(
        string symbol,
        DateTime fromUtc,
        DateTime toUtc,
        BarTimeFrame timeFrame,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, IReadOnlyList<IBar>> items;
        if (symbol.Contains('/'))
        {
            var marketType = BarTimeFrameResolver.GetMarketType(timeFrame);
            var cryptoRequest = new HistoricalCryptoBarsRequest(symbol, fromUtc, toUtc, timeFrame);
            var cryptoResult = await _cryptoClient.GetHistoricalBarsAsync(cryptoRequest, cancellationToken)
                .ConfigureAwait(false);
            items = cryptoResult.Items;
        }
        else
        {
            var stockRequest = new HistoricalBarsRequest(symbol, fromUtc, toUtc, timeFrame);
            var stockResult = await _stockClient.GetHistoricalBarsAsync(stockRequest, cancellationToken)
                .ConfigureAwait(false);
            items = stockResult.Items;
        }

        IReadOnlyList<IBar>? bars = null;
        if (items.TryGetValue(symbol, out var byKey))
            bars = byKey;
        else if (items.TryGetValue(symbol.ToUpperInvariant(), out var byUpper))
            bars = byUpper;
        else if (items.Count == 1)
            bars = items.Values.First();

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