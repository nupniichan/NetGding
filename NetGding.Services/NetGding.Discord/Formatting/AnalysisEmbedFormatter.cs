using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSharpPlus.Entities;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Discord.Formatting;

public sealed record DiscordNewsItem(
    long Id,
    string Symbol,
    string Title,
    string Source,
    string Url,
    DateTime PublishedAtUtc,
    string Summary,
    string? Sentiment = null);

public sealed class AnalysisEmbedFormatter
{
    private const int FieldValueMaxLength = 1024;

    public DiscordEmbed Build(AnalysisResult r)
    {
        var color = r.Decision switch
        {
            TradeDecision.Buy => new DiscordColor(0x00B894),
            TradeDecision.Sell => new DiscordColor(0xD63031),
            TradeDecision.Wait => new DiscordColor(0x95A5A6),
            _ => new DiscordColor(0x95A5A6)
        };

        var builder = new DiscordEmbedBuilder()
            .WithTitle($"NetGding | {r.Symbol} | {NormalizeTimeframe(r.Timeframe)}")
            .WithColor(color)
            .WithTimestamp(r.AnalyzedAtUtc)
            .AddField("Decision", NormalizeDecision(r.Decision), inline: true)
            .AddField("AI Confidence", $"{(r.Confidence * 100):F0}%", inline: true)
            .AddField("Price", r.CurrentPrice.ToString("F2"), inline: true)
            .AddField("Hold Time",
                string.IsNullOrWhiteSpace(r.ExpectedHoldTime) ? "N/A" : r.ExpectedHoldTime,
                inline: true)
            .AddField("Market", $"{NormalizeMarket(r.Market)} / {NormalizeMarketType(r.MarketType)}", inline: true);

        if (r.FearAndGreedIndex.HasValue)
        {
            builder.AddField("Fear & Greed", $"{GetFearAndGreedEmoji(r.FearAndGreedIndex.Value)} {r.FearAndGreedIndex.Value} ({r.FearAndGreedClassification})", inline: true);
        }

        builder.AddField("Trends",
            $"{GetTrendEmoji(r.MarketStructure.ShortTermTrend)} Short-term: {NormalizeTrend(r.MarketStructure.ShortTermTrend)}\n" +
            $"{GetTrendEmoji(r.MarketStructure.MidTermTrend)} Mid-term:   {NormalizeTrend(r.MarketStructure.MidTermTrend)}\n" +
            $"{GetTrendEmoji(r.MarketStructure.LongTermTrend)} Long-term:  {NormalizeTrend(r.MarketStructure.LongTermTrend)}",
            inline: false);

        AppendIndicatorFields(builder, r.Indicators);
        if (r.Decision != TradeDecision.Wait)
            AppendRiskManagementField(builder, r.RiskManagement, r.MarketType);

        if (!string.IsNullOrWhiteSpace(r.Reason))
        {
            var reason = r.Reason.Length > FieldValueMaxLength
                ? r.Reason[..(FieldValueMaxLength - 3)] + "..."
                : r.Reason;
            builder.AddField("Reason", reason);
        }

        return builder.Build();
    }

    private static void AppendIndicatorFields(DiscordEmbedBuilder builder, IndicatorSnapshot indicators)
    {
        var parts = new List<string>();

        var trendParts = new List<string>();
        AppendGroup(trendParts, "EMA", indicators.Ema);
        AppendGroup(trendParts, "VWAP", indicators.Vwap);
        AppendGroup(trendParts, "S/R", indicators.SupportResistance);
        if (trendParts.Count > 0)
        {
            parts.Add("**📈 Trend**");
            parts.AddRange(trendParts);
        }

        var volumeParts = new List<string>();
        AppendGroup(volumeParts, "VolumeMa", indicators.VolumeMa);
        if (volumeParts.Count > 0)
        {
            if (parts.Count > 0) parts.Add(string.Empty);
            parts.Add("**📊 Volume**");
            parts.AddRange(volumeParts);
        }

        var momentumParts = new List<string>();
        AppendGroup(momentumParts, "MACD", indicators.Macd);
        AppendGroup(momentumParts, "RSI", indicators.Rsi);
        AppendGroup(momentumParts, "BB", indicators.BollingerBands);
        AppendGroup(momentumParts, "ATR", indicators.Atr);
        if (momentumParts.Count > 0)
        {
            if (parts.Count > 0) parts.Add(string.Empty);
            parts.Add("**⚡ Momentum**");
            parts.AddRange(momentumParts);
        }

        var indicatorVal = parts.Count > 0 ? string.Join("\n", parts) : "No data";
        indicatorVal = TruncateFieldValue(indicatorVal);

        builder.AddField("Indicators", indicatorVal, inline: false);
    }

    private static string TruncateFieldValue(string value)
    {
        if (value.Length > FieldValueMaxLength)
            return value[..(FieldValueMaxLength - 3)] + "...";
        return value;
    }

    private static void AppendGroup(List<string> parts, string name, Dictionary<string, float> values)
    {
        if (values.Count == 0) return;

        var pairs = string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value:F2}"));
        parts.Add($"**{name}**: {pairs}");
    }

    private static void AppendRiskManagementField(
        DiscordEmbedBuilder builder, RiskManagement risk, MarketType marketType)
    {
        var value = marketType == MarketType.Future
            ? $"**Entry:** {FormatDecimal(risk.Futures?.Entry)}\n" +
              $"**Stop Loss:** {FormatDecimal(risk.Futures?.StopLoss)}\n" +
              $"**Take Profit:** {FormatDecimal(risk.Futures?.TakeProfit)}"
            : $"**Buy Price:** {FormatDecimal(risk.Spot?.BuyPrice)}\n" +
              $"**DCA Levels:** {FormatDcaLevels(risk.Spot?.DcaLevels)}";

        builder.AddField("Risk Management", value);
    }

    private static string FormatDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("F2") : "N/A";

    private static string FormatDcaLevels(IReadOnlyList<decimal>? levels)
    {
        if (levels is null || levels.Count == 0) return "N/A";
        return string.Join(", ", levels.Select(l => l.ToString("F2")));
    }

    private static string NormalizeMarket(AssetMarket market) => market switch
    {
        AssetMarket.Stock => "Stock",
        AssetMarket.Crypto => "Crypto",
        AssetMarket.Forex => "Forex",
        _ => market.ToString()
    };

    private static string NormalizeMarketType(MarketType type) => type switch
    {
        MarketType.Spot => "Spot",
        MarketType.Future => "Future",
        _ => type.ToString()
    };

    private static string NormalizeDecision(TradeDecision decision) => decision switch
    {
        TradeDecision.Buy => "🟢 BUY",
        TradeDecision.Sell => "🔴 SELL",
        TradeDecision.Wait => "🟡 WAIT",
        _ => decision.ToString()
    };

    private static string GetTrendEmoji(TrendDirection trend) => trend switch
    {
        TrendDirection.Uptrend => "🟢",
        TrendDirection.Downtrend => "🔴",
        TrendDirection.Sideways => "🟡",
        _ => "⚪"
    };

    private static string NormalizeTrend(TrendDirection trend) => trend switch
    {
        TrendDirection.Uptrend => "Uptrend",
        TrendDirection.Downtrend => "Downtrend",
        TrendDirection.Sideways => "Sideways",
        _ => trend.ToString()
    };

    private static string NormalizeTimeframe(string tf) => tf.ToLowerInvariant() switch
    {
        "1m" => "M1",
        "5m" => "M5",
        "15m" => "M15",
        "30m" => "M30",
        "1h" => "H1",
        "4h" => "H4",
        "1d" => "D1",
        "1w" => "W1",
        _ => tf.ToUpperInvariant()
    };

    public static string GetFearAndGreedEmoji(int value) => value switch
    {
        <= 25 => "🔴",
        <= 45 => "🟠",
        <= 55 => "🟡",
        <= 75 => "🟢",
        _ => "🚀"
    };

    public DiscordEmbed BuildChartEmbed(AnalysisResult r)
    {
        var color = r.Decision switch
        {
            TradeDecision.Buy => new DiscordColor(0x00B894),
            TradeDecision.Sell => new DiscordColor(0xD63031),
            TradeDecision.Wait => new DiscordColor(0x95A5A6),
            _ => new DiscordColor(0x95A5A6)
        };

        var builder = new DiscordEmbedBuilder()
            .WithTitle($"NetGding Chart | {r.Symbol} | {NormalizeTimeframe(r.Timeframe)}")
            .WithColor(color)
            .WithTimestamp(r.AnalyzedAtUtc)
            .AddField("Price", r.CurrentPrice.ToString("F2"), inline: true);

        if (r.Reason != "Chart Only")
        {
            builder.AddField("Decision", NormalizeDecision(r.Decision), inline: true)
                   .AddField("AI Confidence", $"{(r.Confidence * 100):F0}%", inline: true)
                   .AddField("Hold Time", string.IsNullOrWhiteSpace(r.ExpectedHoldTime) ? "N/A" : r.ExpectedHoldTime, inline: true);
        }

        return builder.Build();
    }

    public DiscordEmbed BuildDomChartEmbed(AnalysisResult r)
    {
        var color = new DiscordColor(0x95A5A6);
        return new DiscordEmbedBuilder()
            .WithTitle($"NetGding DOM Chart | {r.Symbol} | {NormalizeTimeframe(r.Timeframe)}")
            .WithColor(color)
            .WithTimestamp(r.AnalyzedAtUtc)
            .Build();
    }

    public DiscordEmbed BuildNewsEmbed(string symbol, IReadOnlyList<DiscordNewsItem> articles)
    {
        var builder = new DiscordEmbedBuilder()
            .WithTitle($"NetGding News | {symbol.ToUpperInvariant()}")
            .WithColor(new DiscordColor(0x3498DB))
            .WithTimestamp(DateTime.UtcNow);

        if (articles.Count == 0)
        {
            builder.WithDescription("No recent news articles found for this symbol.");
            return builder.Build();
        }

        foreach (var art in articles)
        {
            var sentimentEmoji = art.Sentiment?.ToLowerInvariant() switch
            {
                "bullish" or "positive" => "🟢",
                "bearish" or "negative" => "🔴",
                _ => "⚪"
            };

            var description = $"**Source:** {art.Source} | {sentimentEmoji} {art.Sentiment ?? "Neutral"}\n" +
                              $"**Published:** {art.PublishedAtUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
                              $"[Read Article]({art.Url})\n" +
                              $"{art.Summary}";

            if (description.Length > 1024)
                description = description[..1021] + "...";

            var title = art.Title.Length > 256 ? art.Title[..253] + "..." : art.Title;
            builder.AddField(title, description);
        }

        return builder.Build();
    }
}