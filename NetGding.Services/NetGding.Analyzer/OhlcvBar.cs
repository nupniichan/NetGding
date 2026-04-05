namespace NetGding.Analyzer;

public readonly record struct OhlcvBar(
    DateTime TimestampUtc,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume);