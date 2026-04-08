using System.Text;
using System.Text.RegularExpressions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Telegram.Formatting;

public sealed class AnalysisMessageFormatter
{
    private static readonly Regex s_escapeRegex =
        new(@"([_*\[\]()~`>#+\-=|{}.!\\])", RegexOptions.Compiled);

    public string Build(AnalysisResult r)
    {
        var sb = new StringBuilder();

        sb.Append("*Symbol:* ").Append(Escape(r.Symbol)).Append('\n');
        sb.Append("*Market:* ").Append(Escape(NormalizeMarket(r.Market))).Append('\n');
        sb.Append("*Type:* ").Append(Escape(NormalizeMarketType(r.MarketType))).Append('\n');
        sb.Append('\n');
        sb.Append("*Timeframe:* ").Append(Escape(NormalizeTimeframe(r.Timeframe))).Append('\n');
        sb.Append('\n');
        sb.Append("*Current Price:* ").Append(Escape(r.CurrentPrice.ToString("F2"))).Append('\n');
        sb.Append('\n');
        sb.Append("*Indicators:*").Append('\n');
        AppendIndicators(sb, r.Indicators);
        sb.Append('\n');
        sb.Append("*Market Structure:*").Append('\n');
        sb.Append("\\- Short\\-term Trend: ").Append(Escape(NormalizeTrend(r.MarketStructure.ShortTermTrend))).Append('\n');
        sb.Append("\\- Mid\\-term Trend: ").Append(Escape(NormalizeTrend(r.MarketStructure.MidTermTrend))).Append('\n');
        sb.Append("\\- Long\\-term Trend: ").Append(Escape(NormalizeTrend(r.MarketStructure.LongTermTrend))).Append('\n');
        sb.Append('\n');
        sb.Append("*Decision:* ").Append(Escape(NormalizeDecision(r.Decision))).Append('\n');
        sb.Append('\n');
        sb.Append("*Reason:*").Append('\n');
        sb.Append(Escape(string.IsNullOrWhiteSpace(r.Reason) ? "N/A" : r.Reason)).Append('\n');
        sb.Append('\n');
        sb.Append("*Time Estimate:*").Append('\n');
        sb.Append("\\- Expected Hold Time: ").Append(Escape(string.IsNullOrWhiteSpace(r.ExpectedHoldTime) ? "N/A" : r.ExpectedHoldTime)).Append('\n');
        sb.Append('\n');
        sb.Append("*Risk Management:*").Append('\n');
        AppendRiskManagement(sb, r.RiskManagement, r.MarketType);
        sb.Append('\n');
        sb.Append("*Datetime:* ").Append(Escape(r.AnalyzedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(" UTC");

        return sb.ToString();
    }

    private static void AppendIndicators(StringBuilder sb, IndicatorSnapshot indicators)
    {
        AppendIndicatorGroup(sb, "EMA", indicators.Ema);
        AppendIndicatorGroup(sb, "MACD", indicators.Macd);
        AppendIndicatorGroup(sb, "RSI", indicators.Rsi);
        AppendIndicatorGroup(sb, "BollingerBands", indicators.BollingerBands);
        AppendIndicatorGroup(sb, "ATR", indicators.Atr);
        AppendIndicatorGroup(sb, "VolumeMa", indicators.VolumeMa);
        AppendIndicatorGroup(sb, "VWAP", indicators.Vwap);
    }

    private static void AppendIndicatorGroup(StringBuilder sb, string name, Dictionary<string, float> values)
    {
        if (values.Count == 0) return;

        var pairs = string.Join(", ", values.Select(kv =>
            $"{Escape(kv.Key)}\\={Escape(kv.Value.ToString("F2"))}"));

        sb.Append("  ").Append(Escape(name)).Append(": ").Append(pairs).Append('\n');
    }

    private static void AppendRiskManagement(StringBuilder sb, RiskManagement risk, MarketType marketType)
    {
        if (marketType == MarketType.Future)
        {
            sb.Append("For Futures:").Append('\n');
            sb.Append("\\- Entry: ").Append(FormatDecimal(risk.Futures?.Entry)).Append('\n');
            sb.Append("\\- Stop Loss: ").Append(FormatDecimal(risk.Futures?.StopLoss)).Append('\n');
            sb.Append("\\- Take Profit: ").Append(FormatDecimal(risk.Futures?.TakeProfit)).Append('\n');
        }
        else
        {
            sb.Append("For Spot:").Append('\n');
            sb.Append("\\- Buy Price: ").Append(FormatDecimal(risk.Spot?.BuyPrice)).Append('\n');
            sb.Append("\\- DCA Levels: ").Append(FormatDcaLevels(risk.Spot?.DcaLevels)).Append('\n');
        }
    }

    private static string FormatDecimal(decimal? value) =>
        value.HasValue ? Escape(value.Value.ToString("F2")) : "N/A";

    private static string FormatDcaLevels(IReadOnlyList<decimal>? levels)
    {
        if (levels is null || levels.Count == 0) return "N/A";
        return string.Join(", ", levels.Select(l => Escape(l.ToString("F2"))));
    }

    private static string NormalizeMarket(AssetMarket market) => market switch
    {
        AssetMarket.Stock => "stock",
        AssetMarket.Crypto => "crypto",
        AssetMarket.Forex => "forex",
        _ => market.ToString().ToLowerInvariant()
    };

    private static string NormalizeMarketType(MarketType type) => type switch
    {
        MarketType.Spot => "spot",
        MarketType.Future => "future",
        _ => type.ToString().ToLowerInvariant()
    };

    private static string NormalizeDecision(TradeDecision decision) => decision switch
    {
        TradeDecision.Buy => "buy",
        TradeDecision.Sell => "sell",
        TradeDecision.Wait => "wait",
        _ => decision.ToString().ToLowerInvariant()
    };

    private static string NormalizeTrend(TrendDirection trend) => trend switch
    {
        TrendDirection.Uptrend => "uptrend",
        TrendDirection.Downtrend => "downtrend",
        TrendDirection.Sideways => "sideways",
        _ => trend.ToString().ToLowerInvariant()
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

    internal static string Escape(string text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : s_escapeRegex.Replace(text, @"\$1");
}