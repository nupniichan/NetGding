using System.Globalization;

namespace NetGding.Contracts.Models.MarketData;

public static class MarketParsingHelper
{
    public static bool TryResolveMarketType(string? requested, out MarketType marketType)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            marketType = default;
            return false;
        }

        var normalized = requested.Trim().ToLowerInvariant();
        if (normalized == "spot")
        {
            marketType = MarketType.Spot;
            return true;
        }

        if (normalized is "future" or "futures")
        {
            marketType = MarketType.Future;
            return true;
        }

        marketType = default;
        return false;
    }

    public static double ParseInvariantDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0d;
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
