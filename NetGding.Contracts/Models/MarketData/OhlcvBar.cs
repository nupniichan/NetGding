namespace NetGding.Contracts.Models.MarketData;

// open, high, low, close, volume
public readonly record struct OhlcvBar(
    DateTime TimestampUtc,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume);