using Alpaca.Markets;

namespace NetGding.Collector.Alpaca;

internal static class BarTimeFrameResolver
{
    public static bool TryResolve(string? name, out BarTimeFrame timeFrame)
    {
        timeFrame = BarTimeFrame.Hour;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.Trim().ToUpperInvariant() switch
        {
            "15M" => Set(new BarTimeFrame(15, BarTimeFrameUnit.Minute), out timeFrame),
            "1H" => Set(BarTimeFrame.Hour, out timeFrame),
            "4H" => Set(new BarTimeFrame(4, BarTimeFrameUnit.Hour), out timeFrame),
            "1D" => Set(BarTimeFrame.Day, out timeFrame),
            "1W" => Set(BarTimeFrame.Week, out timeFrame),
            "1M" => Set(BarTimeFrame.Month, out timeFrame),
            _ => false
        };
    }

    private static bool Set(BarTimeFrame value, out BarTimeFrame tf)
    {
        tf = value;
        return true;
    }
}