namespace NetGding.Contracts.Models.Analysis;

public sealed record OnDemandRequest(
    string Symbol,
    string Timeframe,
    string Exchange = "binance",
    string MarketType = "spot",
    string? ChartSymbol = null,
    bool ChartOnly = false);