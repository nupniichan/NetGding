namespace NetGding.Contracts;

public static class ValidationConstants
{
    public static readonly HashSet<string> AllowedTimeframes = new(StringComparer.OrdinalIgnoreCase)
    {
        "15m", "1h", "4h", "1d", "1w", "1m"
    };

    public static readonly HashSet<string> AllowedExchanges = new(StringComparer.OrdinalIgnoreCase)
    {
        "binance", "okx"
    };

    public static readonly HashSet<string> AllowedMarketTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "spot"
    };

    public static string NormalizeSymbol(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return normalized.Contains('/', StringComparison.Ordinal) ? normalized : $"{normalized}/USD";
    }
}
