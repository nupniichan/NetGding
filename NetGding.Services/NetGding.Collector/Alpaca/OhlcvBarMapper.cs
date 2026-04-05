using Alpaca.Markets;
using NetGding.Models.MarketData;

namespace NetGding.Collector.Alpaca;

internal static class OhlcvBarMapper
{
    public static OhlcvBar FromAlpaca(IBar bar) => new(
        bar.TimeUtc,
        (double)bar.Open,
        (double)bar.High,
        (double)bar.Low,
        (double)bar.Close,
        (double)bar.Volume);
}