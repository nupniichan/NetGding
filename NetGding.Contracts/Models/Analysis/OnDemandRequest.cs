namespace NetGding.Contracts.Models.Analysis;

public sealed record OnDemandRequest(
    string Symbol,
    string Timeframe,
    string Exchange,
    string MarketType,
    string? ChartSymbol = null,
    bool ChartOnly = false);