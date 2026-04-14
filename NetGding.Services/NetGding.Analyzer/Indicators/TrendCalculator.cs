using NetGding.Contracts.Models.Indicators.Trends;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Indicators;

public static class TrendCalculator
{
    public static void FillEma(EMA target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        int n = bars.Count;
        foreach (int p in EMA.Periods)
        {
            if (n < p) continue;
            var series = IndicatorMath.EmaCloseSeries(bars, p);
            target.Values[p.ToString()] = (float)series[n - 1];
        }
    }

    public static void FillMacd(MACD target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        var periods = MACD.Periods;
        if (periods.Count < 3) return;
        int fast = periods[0];
        int slow = periods[1];
        int sig = periods[2];
        int n = bars.Count;
        if (n < slow) return;
        var emaF = IndicatorMath.EmaCloseSeries(bars, fast);
        var emaS = IndicatorMath.EmaCloseSeries(bars, slow);
        var macd = new double[n];
        for (int i = slow - 1; i < n; i++)
            macd[i] = emaF[i] - emaS[i];
        int i0 = slow - 1;
        int last = n - 1;
        if (last < i0 + sig - 1) return;
        double sum = 0;
        for (int j = 0; j < sig; j++)
            sum += macd[i0 + j];
        double sigEma = sum / sig;
        double a = 2.0 / (sig + 1);
        for (int i = i0 + sig; i <= last; i++)
            sigEma = a * macd[i] + (1 - a) * sigEma;
        double line = macd[last];
        target.Values["Line"] = (float)line;
        target.Values["Signal"] = (float)sigEma;
        target.Values["Histogram"] = (float)(line - sigEma);
    }
}