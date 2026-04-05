using NetGding.Models.Indicators.Momentum;

namespace NetGding.Analyzer.Indicators;

public static class MomentumCalculator
{
    public static void FillRsi(RSI target, IReadOnlyList<OhlcvBar> bars)
    {
        target.Values.Clear();
        int period = RSI.Periods[0];
        int n = bars.Count;
        if (n <= period) return;
        double avgGain = 0;
        double avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            double ch = bars[i].Close - bars[i - 1].Close;
            if (ch > 0) avgGain += ch;
            else avgLoss -= ch;
        }
        avgGain /= period;
        avgLoss /= period;
        for (int i = period + 1; i < n; i++)
        {
            double ch = bars[i].Close - bars[i - 1].Close;
            double g = ch > 0 ? ch : 0;
            double l = ch < 0 ? -ch : 0;
            avgGain = (avgGain * (period - 1) + g) / period;
            avgLoss = (avgLoss * (period - 1) + l) / period;
        }
        float rsi;
        if (avgLoss <= 1e-12)
            rsi = avgGain <= 1e-12 ? 50f : 100f;
        else
        {
            double rs = avgGain / avgLoss;
            rsi = (float)(100.0 - 100.0 / (1.0 + rs));
        }
        target.Values[period.ToString()] = rsi;
    }
}