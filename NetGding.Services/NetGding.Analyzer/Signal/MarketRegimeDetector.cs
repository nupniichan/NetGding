using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Analyzer.Signal;

public static class MarketRegimeDetector
{
    private const double VolatileAtrThreshold = 0.02;
    private const double TrendingEmaSpreadThreshold = 0.005;

    public static MarketRegime Detect(IndicatorSnapshot indicators, double currentPrice)
    {
        if (currentPrice <= 0) return MarketRegime.Ranging;

        if (IsVolatile(indicators, currentPrice))
            return MarketRegime.Volatile;

        if (IsTrending(indicators))
            return MarketRegime.Trending;

        return MarketRegime.Ranging;
    }

    private static bool IsVolatile(IndicatorSnapshot indicators, double currentPrice)
    {
        if (indicators.Atr.Count == 0) return false;

        var atr = (double)indicators.Atr.Values.Max();
        return atr / currentPrice > VolatileAtrThreshold;
    }

    private static bool IsTrending(IndicatorSnapshot indicators)
    {
        var emaSpreadSignificant = HasSignificantEmaSpread(indicators);
        var macdDirectional = IsMacdDirectional(indicators);

        return emaSpreadSignificant && macdDirectional;
    }

    private static bool HasSignificantEmaSpread(IndicatorSnapshot indicators)
    {
        if (!indicators.Ema.TryGetValue("9", out var fast) ||
            !indicators.Ema.TryGetValue("21", out var slow))
            return false;

        if (slow == 0) return false;

        var spread = Math.Abs((double)(fast - slow) / (double)slow);
        return spread > TrendingEmaSpreadThreshold;
    }

    private static bool IsMacdDirectional(IndicatorSnapshot indicators)
    {
        if (!indicators.Macd.TryGetValue("Histogram", out var histogram))
            return false;

        return Math.Abs(histogram) > 0f;
    }
}
