using DSharpPlus.Entities;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Discord.Formatting;

public sealed class AnalysisEmbedFormatter
{
    private const int FieldValueMaxLength = 1024;

    public DiscordEmbed Build(AnalysisResult r)
    {
        var color = r.Decision switch
        {
            TradeDecision.Buy => new DiscordColor(0x00B894),
            TradeDecision.Sell => new DiscordColor(0xD63031),
            _ => new DiscordColor(0xFDCB6E)
        };

        var builder = new DiscordEmbedBuilder()
            .WithTitle($"NetGding | {r.Symbol} | {NormalizeTimeframe(r.Timeframe)}")
            .WithColor(color)
            .WithTimestamp(r.AnalyzedAtUtc)
            .AddField("Decision", NormalizeDecision(r.Decision), inline: true)
            .AddField("Price", r.CurrentPrice.ToString("F2"), inline: true)
            .AddField("Market", $"{NormalizeMarket(r.Market)} / {NormalizeMarketType(r.MarketType)}", inline: true)
            .AddField("Trends",
                $"Short-term: {NormalizeTrend(r.MarketStructure.ShortTermTrend)}\n" +
                $"Mid-term:   {NormalizeTrend(r.MarketStructure.MidTermTrend)}\n" +
                $"Long-term:  {NormalizeTrend(r.MarketStructure.LongTermTrend)}")
            .AddField("Hold Time",
                string.IsNullOrWhiteSpace(r.ExpectedHoldTime) ? "N/A" : r.ExpectedHoldTime,
                inline: true);

        AppendIndicatorField(builder, r.Indicators);
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

    private static void AppendIndicatorField(DiscordEmbedBuilder builder, IndicatorSnapshot indicators)
    {
        var parts = new List<string>();

        AppendGroup(parts, "EMA", indicators.Ema);
        AppendGroup(parts, "MACD", indicators.Macd);
        AppendGroup(parts, "RSI", indicators.Rsi);
        AppendGroup(parts, "BB", indicators.BollingerBands);
        AppendGroup(parts, "ATR", indicators.Atr);
        AppendGroup(parts, "VolumeMa", indicators.VolumeMa);
        AppendGroup(parts, "VWAP", indicators.Vwap);

        if (parts.Count == 0) return;

        var value = string.Join("\n", parts);
        if (value.Length > FieldValueMaxLength)
            value = value[..(FieldValueMaxLength - 3)] + "...";

        builder.AddField("Indicators", value);
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
        TradeDecision.Buy => "BUY",
        TradeDecision.Sell => "SELL",
        TradeDecision.Wait => "WAIT",
        _ => decision.ToString()
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
}