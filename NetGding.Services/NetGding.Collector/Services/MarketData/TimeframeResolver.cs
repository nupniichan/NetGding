using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services.MarketData;

internal enum CandleIntervalUnit
{
    Minute,
    Hour,
    Day,
    Week,
    Month
}

internal readonly record struct CandleTimeFrame(int Value, CandleIntervalUnit Unit);

internal static class TimeframeResolver
{
    public static bool IsAutoScheduled(string tfName) =>
        tfName.Trim().ToUpperInvariant() is "1D" or "1W" or "1M";

    public static bool TryResolve(string? name, out CandleTimeFrame timeFrame)
    {
        timeFrame = new CandleTimeFrame(1, CandleIntervalUnit.Hour);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Trim().ToUpperInvariant() switch
        {
            "15M" => Set(new CandleTimeFrame(15, CandleIntervalUnit.Minute), out timeFrame),
            "1H" => Set(new CandleTimeFrame(1, CandleIntervalUnit.Hour), out timeFrame),
            "4H" => Set(new CandleTimeFrame(4, CandleIntervalUnit.Hour), out timeFrame),
            "1D" => Set(new CandleTimeFrame(1, CandleIntervalUnit.Day), out timeFrame),
            "1W" => Set(new CandleTimeFrame(1, CandleIntervalUnit.Week), out timeFrame),
            "1M" => Set(new CandleTimeFrame(1, CandleIntervalUnit.Month), out timeFrame),
            _ => false
        };
    }

    public static MarketType DefaultMarketType(CandleTimeFrame timeFrame)
    {
        return timeFrame.Unit switch
        {
            CandleIntervalUnit.Minute => MarketType.Future,
            CandleIntervalUnit.Hour when timeFrame.Value < 4 => MarketType.Future,
            _ => MarketType.Spot
        };
    }

    public static TimeSpan DelayUntilNextBarBoundaryUtc(CandleTimeFrame tf, DateTime utcNow)
    {
        var next = NextBarBoundaryUtcStrictlyAfter(tf, utcNow);
        var d = next - utcNow;
        if (d < TimeSpan.FromSeconds(1))
            d = TimeSpan.FromSeconds(1);
        return d;
    }

    private static bool Set(CandleTimeFrame value, out CandleTimeFrame tf)
    {
        tf = value;
        return true;
    }

    private static DateTime NextBarBoundaryUtcStrictlyAfter(CandleTimeFrame tf, DateTime utcNow)
    {
        var v = Math.Max(1, tf.Value);
        return tf.Unit switch
        {
            CandleIntervalUnit.Minute => NextMinuteBoundaryUtc(utcNow, v),
            CandleIntervalUnit.Hour => NextHourBoundaryUtc(utcNow, v),
            CandleIntervalUnit.Day => NextDayBoundaryFromEpochUtc(utcNow, v),
            CandleIntervalUnit.Week => NextWeekBoundaryFromEpochUtc(utcNow, v),
            CandleIntervalUnit.Month => NextCalendarMonthStartUtc(utcNow),
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
