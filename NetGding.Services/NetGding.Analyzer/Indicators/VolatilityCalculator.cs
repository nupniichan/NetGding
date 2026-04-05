using NetGding.Models.Indicators.Volatility;
using NetGding.Models.MarketData;

namespace NetGding.Analyzer.Indicators;

public static class VolatilityCalculator
{
    public static void FillBollingerBands(BollingerBands target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        int p = BollingerBands.Periods[0];
        double mult = BollingerBands.StandardDeviationMultiplier;
        int n = bars.Count;
        if (n < p) return;
        double sum = 0;
        for (int i = n - p; i < n; i++)
            sum += bars[i].Close;
        double middle = sum / p;
        double varSum = 0;
        for (int i = n - p; i < n; i++)
        {
            double d = bars[i].Close - middle;
            varSum += d * d;
        }
        double std = Math.Sqrt(varSum / p);
        double u = middle + mult * std;
        double l = middle - mult * std;
        target.Values["Middle"] = (float)middle;
        target.Values["Upper"] = (float)u;
        target.Values["Lower"] = (float)l;
    }

    public static void FillAtr(ATR target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        int period = ATR.Periods[0];
        int n = bars.Count;
        if (n < period + 1) return;
        var tr = new double[n];
        tr[0] = bars[0].High - bars[0].Low;
        for (int i = 1; i < n; i++)
        {
            double pc = bars[i - 1].Close;
            double a = bars[i].High - bars[i].Low;
            double b = Math.Abs(bars[i].High - pc);
            double c = Math.Abs(bars[i].Low - pc);
            tr[i] = Math.Max(a, Math.Max(b, c));
        }
        double sum = 0;
        for (int i = 1; i <= period; i++)
            sum += tr[i];
        double atr = sum / period;
        for (int i = period + 1; i < n; i++)
            atr = (atr * (period - 1) + tr[i]) / period;
        target.Values[period.ToString()] = (float)atr;
    }
}