namespace NetGding.Contracts.Models.MarketData;

public readonly record struct OhlcvSeries(
    string Symbol,
    string BarTimeFrame,
    IReadOnlyList<OhlcvBar> Bars);