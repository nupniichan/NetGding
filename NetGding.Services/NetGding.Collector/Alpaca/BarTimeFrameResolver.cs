using Alpaca.Markets;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Alpaca;

internal static class BarTimeFrameResolver
{
    public static MarketType GetMarketType(BarTimeFrame timeFrame)
    {
        // Spot: >= 4H. Future: < 4H
        return timeFrame.Unit switch
        {
            BarTimeFrameUnit.Minute => MarketType.Future,
            BarTimeFrameUnit.Hour when timeFrame.Value < 4 => MarketType.Future,
            BarTimeFrameUnit.Hour => MarketType.Spot,
            BarTimeFrameUnit.Day => MarketType.Spot,
            BarTimeFrameUnit.Week => MarketType.Spot,
            BarTimeFrameUnit.Month => MarketType.Spot,
            _ => MarketType.Spot
        };
    }

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

    public static TimeSpan DelayUntilNextBarBoundaryUtc(BarTimeFrame tf, DateTime utcNow)
    {
        var next = NextBarBoundaryUtcStrictlyAfter(tf, utcNow);
        var d = next - utcNow;
        if (d < TimeSpan.FromSeconds(1))
            d = TimeSpan.FromSeconds(1);
        return d;
    }

    private static DateTime NextBarBoundaryUtcStrictlyAfter(BarTimeFrame tf, DateTime utcNow)
    {
        var v = Math.Max(1, tf.Value);
        return tf.Unit switch
        {
            BarTimeFrameUnit.Minute => NextMinuteBoundaryUtc(utcNow, v),
            BarTimeFrameUnit.Hour => NextHourBoundaryUtc(utcNow, v),
            BarTimeFrameUnit.Day => NextDayBoundaryFromEpochUtc(utcNow, v),
            BarTimeFrameUnit.Week => NextWeekBoundaryFromEpochUtc(utcNow, v),
            BarTimeFrameUnit.Month => NextCalendarMonthStartUtc(utcNow),
            _ => utcNow.AddHours(1)
        };
    }

    private static DateTime NextMinuteBoundaryUtc(DateTime utcNow, int periodMinutes)
    {
        var dayStart = utcNow.Date;
        var elapsed = utcNow - dayStart;
        var block = (long)(elapsed.TotalMinutes / periodMinutes) * periodMinutes;
        var next = dayStart.AddMinutes(block + periodMinutes);
        if (next <= utcNow)
            next = next.AddMinutes(periodMinutes);
        return next;
    }

    private static DateTime NextHourBoundaryUtc(DateTime utcNow, int periodHours)
    {
        var dayStart = utcNow.Date;
        var elapsedHours = (utcNow - dayStart).TotalHours;
        var slot = (long)Math.Floor(elapsedHours / periodHours);
        var next = dayStart.AddHours((slot + 1) * periodHours);
        if (next <= utcNow)
            next = next.AddHours(periodHours);
        return next;
    }

    private static DateTime NextDayBoundaryFromEpochUtc(DateTime utcNow, int dayPeriod)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var elapsed = utcNow - epoch;
        var periods = (long)Math.Floor(elapsed.TotalDays / dayPeriod);
        return epoch.AddDays((periods + 1) * dayPeriod);
    }

    private static DateTime NextWeekBoundaryFromEpochUtc(DateTime utcNow, int weekMultiplier)
    {
        var periodDays = 7 * weekMultiplier;
        return NextDayBoundaryFromEpochUtc(utcNow, periodDays);
    }

    private static DateTime NextCalendarMonthStartUtc(DateTime utcNow)
    {
        var next = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        while (next <= utcNow)
            next = next.AddMonths(1);
        return next;
    }
}