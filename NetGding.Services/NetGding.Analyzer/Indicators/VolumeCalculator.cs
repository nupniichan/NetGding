using NetGding.Models.Indicators.Volume;
using NetGding.Models.MarketData;

namespace NetGding.Analyzer.Indicators;

public static class VolumeCalculator
{
    public static void FillVolumeMa(Volume target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        int n = bars.Count;
        foreach (int p in Volume.Periods)
        {
            if (n < p) continue;
            double sum = 0;
            for (int i = n - p; i < n; i++)
                sum += bars[i].Volume;
            target.Values[p.ToString()] = (float)(sum / p);
        }
    }

    public static void FillVwap(VWAP target, IReadOnlyList<OhlcvBar> bars, bool resetEachUtcDay = true)
    {
        target.Values.Clear();
        int n = bars.Count;
        if (n == 0) return;
        double cumTpv = 0;
        double cumV = 0;
        int? dayKey = null;
        for (int i = 0; i < n; i++)
        {
            var b = bars[i];
            if (resetEachUtcDay)
            {
                int dk = b.TimestampUtc.Year * 1000 + b.TimestampUtc.DayOfYear;
                if (dayKey != dk)
                {
                    dayKey = dk;
                    cumTpv = 0;
                    cumV = 0;
                }
            }
            double tp = (b.High + b.Low + b.Close) / 3.0;
            double v = b.Volume;
            cumTpv += tp * v;
            cumV += v;
            if (cumV > 0)
                target.Values["VWAP"] = (float)(cumTpv / cumV);
        }
    }
}