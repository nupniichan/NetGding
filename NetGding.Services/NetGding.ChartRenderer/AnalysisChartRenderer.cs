using ScottPlot;
using ScottPlot.Finance;
using SkiaSharp;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.ChartRenderer;

public sealed class AnalysisChartRenderer : IChartRenderer
{
    private const int Width = 1200;
    private const int MainHeight = 500;
    private const int VolumeHeight = 150;

    // TradingView dark theme palette
    private const string BgFigure   = "#131722";
    private const string BgData     = "#1e222d";
    private const string GridColor  = "#363c4e";
    private const string AxisColor  = "#787b86";
    private const string CandleUp   = "#26a69a";
    private const string CandleDown = "#ef5350";
    private const string PriceLine  = "#2962ff";
    private const string EmaColors0 = "#f6c35a";
    private const string EmaColors1 = "#2196f3";
    private const string EmaColors2 = "#ab47bc";
    private const string EmaColors3 = "#ff7043";
    private const string EmaColors4 = "#26c6da";
    private const string BbColor    = "#5c6bc0";
    private const string VwapColor  = "#00bcd4";
    private const string SColor     = "#26a69a";
    private const string RColor     = "#ef5350";

    private static readonly string[] EmaColorList = [EmaColors0, EmaColors1, EmaColors2, EmaColors3, EmaColors4];

    public byte[] Render(IReadOnlyList<OhlcvBar> bars, AnalysisResult result)
    {
        if (bars.Count == 0)
            return [];

        var candleSpan = ResolveCandleTimespan(result.Timeframe);
        var mainBytes   = RenderMainPane(bars, result, candleSpan);
        var volumeBytes = RenderVolumePane(bars);

        return CombineVertically([mainBytes, volumeBytes]);
    }

    private static byte[] RenderMainPane(IReadOnlyList<OhlcvBar> bars, AnalysisResult result, TimeSpan candleSpan)
    {
        var plot = new Plot();
        ApplyTheme(plot);

        int n = bars.Count;
        var ohlcs = bars.Select(b => new OHLC(b.Open, b.High, b.Low, b.Close, b.TimestampUtc, candleSpan)).ToList();

        var cs = plot.Add.Candlestick(ohlcs);
        cs.RisingColor  = Color.FromHex(CandleUp);
        cs.FallingColor = Color.FromHex(CandleDown);

        double[] xs     = bars.Select(b => b.TimestampUtc.ToOADate()).ToArray();
        double[] closes = bars.Select(b => b.Close).ToArray();

        var emaPeriods = result.Indicators.Ema.Keys
            .Select(k => int.TryParse(k, out var p) ? p : 0)
            .Where(p => p > 0)
            .OrderBy(p => p)
            .ToList();

        for (int i = 0; i < emaPeriods.Count; i++)
        {
            var color = Color.FromHex(EmaColorList[i % EmaColorList.Length]);
            AddEmaLine(plot, xs, closes, emaPeriods[i], n, color, $"EMA{emaPeriods[i]}");
        }

        AddBollingerBands(plot, xs, closes, 20, 2.0, n);

        // VWAP overlay only when present (intraday)
        if (result.Indicators.Vwap.TryGetValue("VWAP", out var vwapVal))
        {
            var vl = plot.Add.HorizontalLine(vwapVal);
            vl.Color       = Color.FromHex(VwapColor);
            vl.LinePattern = LinePattern.Dashed;
            vl.LineWidth   = 1.5f;
            vl.LegendText  = "VWAP";
        }

        // Current price line
        if (bars.Count > 0)
        {
            var cp = plot.Add.HorizontalLine(bars[^1].Close);
            cp.Color       = Color.FromHex(PriceLine);
            cp.LinePattern = LinePattern.Dashed;
            cp.LineWidth   = 1f;
        }

        // Support / Resistance lines
        foreach (var (key, val) in result.Indicators.SupportResistance)
        {
            bool isSupport = key.StartsWith('S');
            var srLine = plot.Add.HorizontalLine(val);
            srLine.Color       = isSupport ? Color.FromHex(SColor).WithAlpha(0.55f) : Color.FromHex(RColor).WithAlpha(0.55f);
            srLine.LinePattern = LinePattern.Dotted;
            srLine.LineWidth   = 1.2f;
            srLine.LegendText  = key;
        }

        if (result.Decision != TradeDecision.Wait)
            AddRiskLines(plot, result);

        AddDecisionMarker(plot, bars, result);

        AddTopLeftInfo(plot, bars, result);
        plot.Axes.DateTimeTicksBottom();
        plot.XLabel("Date (UTC)");
        plot.YLabel("Price");
        return GetPngBytes(plot, Width, MainHeight);
    }

    private static byte[] RenderVolumePane(IReadOnlyList<OhlcvBar> bars)
    {
        var plot = new Plot();
        ApplyTheme(plot);

        int n = bars.Count;
        double[] xs = bars.Select(b => b.TimestampUtc.ToOADate()).ToArray();

        var barList = new List<ScottPlot.Bar>(n);
        for (int i = 0; i < n; i++)
        {
            barList.Add(new ScottPlot.Bar
            {
                Position  = xs[i],
                Value     = bars[i].Volume,
                FillColor = bars[i].Close >= bars[i].Open
                    ? Color.FromHex(CandleUp).WithAlpha(0.7f)
                    : Color.FromHex(CandleDown).WithAlpha(0.7f)
            });
        }

        plot.Add.Bars(barList);
        plot.Axes.DateTimeTicksBottom();
        plot.XLabel("Date (UTC)");
        plot.YLabel("Volume");

        return GetPngBytes(plot, Width, VolumeHeight);
    }

    private static void AddEmaLine(Plot plot, double[] xs, double[] closes, int period, int n, Color color, string label)
    {
        if (n < period) return;

        double[] ema    = ComputeEma(closes, period);
        var validXs     = xs[(period - 1)..];
        var validEma    = ema[(period - 1)..];

        var line = plot.Add.ScatterLine(validXs, validEma);
        line.Color      = color;
        line.LineWidth  = 1.5f;
        line.LegendText = label;
    }

    private static void AddBollingerBands(Plot plot, double[] xs, double[] closes, int period, double mult, int n)
    {
        if (n < period) return;

        var (upper, middle, lower) = ComputeBollingerBandSeries(closes, period, mult);

        var validXs     = xs[(period - 1)..];
        var validUpper  = upper[(period - 1)..];
        var validMiddle = middle[(period - 1)..];
        var validLower  = lower[(period - 1)..];

        var bbColor = Color.FromHex(BbColor);

        var upperLine = plot.Add.ScatterLine(validXs, validUpper);
        upperLine.Color      = bbColor.WithAlpha(0.65f);
        upperLine.LineWidth  = 1f;
        upperLine.LegendText = "BB";

        var middleLine = plot.Add.ScatterLine(validXs, validMiddle);
        middleLine.Color     = bbColor.WithAlpha(0.35f);
        middleLine.LineWidth = 1f;

        var lowerLine = plot.Add.ScatterLine(validXs, validLower);
        lowerLine.Color     = bbColor.WithAlpha(0.65f);
        lowerLine.LineWidth = 1f;
    }

    private static void AddRiskLines(Plot plot, AnalysisResult result)
    {
        if (result.MarketType == MarketType.Future && result.RiskManagement.Futures is { } futures)
        {
            if (futures.Entry > 0)
            {
                var entry = plot.Add.HorizontalLine((double)futures.Entry);
                entry.Color       = Color.FromHex(EmaColors0);
                entry.LinePattern = LinePattern.Dashed;
                entry.LineWidth   = 1.5f;
                entry.LegendText  = "Entry";
            }
            if (futures.StopLoss > 0)
            {
                var sl = plot.Add.HorizontalLine((double)futures.StopLoss);
                sl.Color       = Color.FromHex(CandleDown);
                sl.LinePattern = LinePattern.Dashed;
                sl.LineWidth   = 1.5f;
                sl.LegendText  = "SL";
            }
            if (futures.TakeProfit > 0)
            {
                var tp = plot.Add.HorizontalLine((double)futures.TakeProfit);
                tp.Color       = Color.FromHex(CandleUp);
                tp.LinePattern = LinePattern.Dashed;
                tp.LineWidth   = 1.5f;
                tp.LegendText  = "TP";
            }
        }
        else if (result.MarketType == MarketType.Spot && result.RiskManagement.Spot is { } spot)
        {
            if (spot.BuyPrice > 0)
            {
                var buy = plot.Add.HorizontalLine((double)spot.BuyPrice);
                buy.Color       = Color.FromHex(EmaColors0);
                buy.LinePattern = LinePattern.Dashed;
                buy.LineWidth   = 1.5f;
                buy.LegendText  = "Buy";
            }
        }
    }

    private static void AddDecisionMarker(Plot plot, IReadOnlyList<OhlcvBar> bars, AnalysisResult result)
    {
        var lastBar = bars[^1];
        double x = lastBar.TimestampUtc.ToOADate();
        double y = lastBar.Close;

        var markerColor = result.Decision switch
        {
            TradeDecision.Buy  => Color.FromHex(CandleUp),
            TradeDecision.Sell => Color.FromHex(CandleDown),
            _                  => Color.FromHex(EmaColors0)
        };

        var marker = plot.Add.Marker(x, y);
        marker.Size  = 14;
        marker.Color = markerColor;
        marker.Shape = MarkerShape.FilledSquare;
    }

    private static void AddTopLeftInfo(Plot plot, IReadOnlyList<OhlcvBar> bars, AnalysisResult result)
    {
        double x = bars[0].TimestampUtc.ToOADate();
        double high = bars.Max(b => b.High);
        double low = bars.Min(b => b.Low);
        double y = high + (high - low) * 0.04;

        var info = plot.Add.Text($"{result.Symbol} | {NormalizeTimeframe(result.Timeframe)}", x, y);
        info.Alignment = Alignment.UpperLeft;
        info.LabelFontColor = Color.FromHex("#d1d4dc");
        info.LabelBackgroundColor = Color.FromHex(BgFigure).WithAlpha(0.78f);
        info.LabelBorderColor = Color.FromHex(BgFigure).WithAlpha(0f);
        info.LabelBold = true;
        info.LabelFontSize = 16;
    }

    private static void ApplyTheme(Plot plot)
    {
        plot.FigureBackground.Color = Color.FromHex(BgFigure);
        plot.DataBackground.Color   = Color.FromHex(BgData);
        plot.Grid.MajorLineColor    = Color.FromHex(GridColor);
        plot.Grid.MinorLineColor    = Color.FromHex(GridColor);

        var axisColor = Color.FromHex(AxisColor);
        plot.Axes.Bottom.FrameLineStyle.Color   = axisColor;
        plot.Axes.Left.FrameLineStyle.Color     = axisColor;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = axisColor;
        plot.Axes.Left.TickLabelStyle.ForeColor   = axisColor;
        plot.Axes.Bottom.Label.ForeColor          = axisColor;
        plot.Axes.Left.Label.ForeColor            = axisColor;
        plot.Legend.IsVisible = false;
    }

    private static byte[] GetPngBytes(Plot plot, int width, int height)
    {
        var image = plot.GetImage(width, height);
        return image.GetImageBytes();
    }

    private static byte[] CombineVertically(IReadOnlyList<byte[]> pngs)
    {
        var bitmaps = pngs.Select(SKBitmap.Decode).ToList();
        try
        {
            int width       = bitmaps.Max(b => b.Width);
            int totalHeight = bitmaps.Sum(b => b.Height);

            using var combined = new SKBitmap(width, totalHeight);
            using var canvas   = new SKCanvas(combined);

            int y = 0;
            foreach (var bmp in bitmaps)
            {
                canvas.DrawBitmap(bmp, 0, y);
                y += bmp.Height;
            }

            using var data = combined.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            foreach (var bmp in bitmaps)
                bmp.Dispose();
        }
    }

    private static double[] ComputeEma(double[] closes, int period)
    {
        int n = closes.Length;
        var ema = new double[n];
        if (n < period) return ema;

        double sum = 0;
        for (int i = 0; i < period; i++)
            sum += closes[i];
        ema[period - 1] = sum / period;

        double alpha = 2.0 / (period + 1);
        for (int i = period; i < n; i++)
            ema[i] = alpha * closes[i] + (1 - alpha) * ema[i - 1];

        return ema;
    }

    private static (double[] upper, double[] middle, double[] lower) ComputeBollingerBandSeries(
        double[] closes, int period, double mult)
    {
        int n = closes.Length;
        var upper  = new double[n];
        var middle = new double[n];
        var lower  = new double[n];

        for (int i = period - 1; i < n; i++)
        {
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++)
                sum += closes[j];
            double avg = sum / period;

            double varSum = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                double d = closes[j] - avg;
                varSum += d * d;
            }
            double std = Math.Sqrt(varSum / period);

            middle[i] = avg;
            upper[i]  = avg + mult * std;
            lower[i]  = avg - mult * std;
        }

        return (upper, middle, lower);
    }

    private static TimeSpan ResolveCandleTimespan(string timeframe) =>
        timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" => TimeSpan.FromMinutes(15),
            "1h" or "1hour" or "60m" => TimeSpan.FromHours(1),
            "4h" or "4hour" or "240m" => TimeSpan.FromHours(4),
            "1d" or "1day" or "d" => TimeSpan.FromDays(1),
            "1w" or "1week" or "w" => TimeSpan.FromDays(7),
            "1m" or "1month" or "mo" => TimeSpan.FromDays(30),
            _ => TimeSpan.FromHours(1)
        };

    private static string NormalizeDecision(TradeDecision d) => d switch
    {
        TradeDecision.Buy  => "BUY",
        TradeDecision.Sell => "SELL",
        _                  => "WAIT"
    };

    private static string NormalizeTimeframe(string tf) => tf.ToLowerInvariant() switch
    {
        "15m" => "M15",
        "1h"  => "H1",
        "4h"  => "H4",
        "1d"  => "D1",
        "1w"  => "W1",
        "1m"  => "MN",
        _     => tf.ToUpperInvariant()
    };
}
