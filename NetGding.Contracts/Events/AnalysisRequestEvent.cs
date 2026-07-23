namespace NetGding.Contracts.Events;

public sealed record AnalysisRequestEvent(
    string RequestId,
    string Symbol,
    string Timeframe,
    string Exchange,
    string MarketType,
    string? ChartSymbol,
    bool ChartOnly,
    DateTime RequestedAtUtc);
