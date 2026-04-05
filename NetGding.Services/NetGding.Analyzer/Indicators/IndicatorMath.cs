using NetGding.Models.MarketData;

namespace NetGding.Analyzer.Indicators;

internal static class IndicatorMath
{
    public static double[] EmaCloseSeries(IReadOnlyList<OhlcvBar> bars, int period)
    {
        int n = bars.Count;
        var ema = new double[n];
        if (n < period) return ema;
        double sum = 0;
        for (int i = 0; i < period; i++)
            sum += bars[i].Close;
        ema[period - 1] = sum / period;
        double alpha = 2.0 / (period + 1);
        for (int i = period; i < n; i++)
            ema[i] = alpha * bars[i].Close + (1 - alpha) * ema[i - 1];
        return ema;
    }
}