using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Analyzer.Indicators;

public static class SupportResistanceCalculator
{
    private const int MaxLevels = 3;

    public static void Fill(IndicatorSnapshot target, IReadOnlyList<OhlcvBar> bars, string timeframe, double atr)
    {
        target.SupportResistance.Clear();

        int n = bars.Count;
        if (n < 10 || atr <= 0) return;

        int wing = IsIntraday(timeframe) ? 5 : 10;
        if (n < wing * 2 + 1) return;

        double currentPrice = bars[n - 1].Close;
        double clusterThreshold = atr / 2.0;

        var rawHighs = DetectSwingHighs(bars, wing);
        var rawLows = DetectSwingLows(bars, wing);

        var resistances = Cluster(rawHighs, clusterThreshold)
            .Where(v => v > currentPrice)
            .OrderBy(v => v)
            .Take(MaxLevels)
            .ToList();

        var supports = Cluster(rawLows, clusterThreshold)
            .Where(v => v < currentPrice)
            .OrderByDescending(v => v)
            .Take(MaxLevels)
            .ToList();

        for (int i = 0; i < supports.Count; i++)
            target.SupportResistance[$"S{i + 1}"] = (float)supports[i];

        for (int i = 0; i < resistances.Count; i++)
            target.SupportResistance[$"R{i + 1}"] = (float)resistances[i];
    }

    private static List<double> DetectSwingHighs(IReadOnlyList<OhlcvBar> bars, int wing)
    {
        int n = bars.Count;
        var highs = new List<double>();

        for (int i = wing; i < n - wing; i++)
        {
            double h = bars[i].High;
            bool isSwingHigh = true;

            for (int j = i - wing; j < i; j++)
                if (bars[j].High >= h) { isSwingHigh = false; break; }

            if (!isSwingHigh) continue;

            for (int j = i + 1; j <= i + wing; j++)
                if (bars[j].High >= h) { isSwingHigh = false; break; }

            if (isSwingHigh)
                highs.Add(h);
        }

        return highs;
    }

    private static List<double> DetectSwingLows(IReadOnlyList<OhlcvBar> bars, int wing)
    {
        int n = bars.Count;
        var lows = new List<double>();

        for (int i = wing; i < n - wing; i++)
        {
            double l = bars[i].Low;
            bool isSwingLow = true;

            for (int j = i - wing; j < i; j++)
                if (bars[j].Low <= l) { isSwingLow = false; break; }

            if (!isSwingLow) continue;

            for (int j = i + 1; j <= i + wing; j++)
                if (bars[j].Low <= l) { isSwingLow = false; break; }

            if (isSwingLow)
                lows.Add(l);
        }

        return lows;
    }

    private static List<double> Cluster(List<double> levels, double threshold)
    {
        if (levels.Count == 0) return [];

        levels.Sort();

        var clusters = new List<List<double>> { new() { levels[0] } };

        for (int i = 1; i < levels.Count; i++)
        {
            var current = clusters[^1];
            if (levels[i] - current[^1] <= threshold)
                current.Add(levels[i]);
            else
                clusters.Add([levels[i]]);
        }

        return clusters.Select(c => c.Average()).ToList();
    }

    private static bool IsIntraday(string timeframe) =>
        timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" or "1h" or "1hour" or "4h" or "4hour" => true,
            _ => false
        };
}
