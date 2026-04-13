using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Indicators;
using NetGding.Analyzer.Llm;
using NetGding.Analyzer.Signal;
using NetGding.Collector.Alpaca;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;
using NetGding.Models.Indicators.Momentum;
using NetGding.Models.Indicators.Trends;
using NetGding.Models.Indicators.Volatility;
using NetGding.Models.Indicators.Volume;

namespace NetGding.Collector.Services;

public sealed class OnDemandAnalyzer : IOnDemandAnalyzer
{
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IAlpacaOhlcvCollector _ohlcvCollector;
    private readonly IAlpacaNewsCollector _newsCollector;
    private readonly ILlmAnalyzer _llm;
    private readonly ISignalEngine _signalEngine;
    private readonly RiskCalculator _riskCalculator;
    private readonly ILogger<OnDemandAnalyzer> _logger;

    public OnDemandAnalyzer(
        IOptionsMonitor<CollectorOptions> options,
        IAlpacaOhlcvCollector ohlcvCollector,
        IAlpacaNewsCollector newsCollector,
        ILlmAnalyzer llm,
        ISignalEngine signalEngine,
        RiskCalculator riskCalculator,
        ILogger<OnDemandAnalyzer> logger)
    {
        _options = options;
        _ohlcvCollector = ohlcvCollector;
        _newsCollector = newsCollector;
        _llm = llm;
        _signalEngine = signalEngine;
        _riskCalculator = riskCalculator;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        if (!BarTimeFrameResolver.TryResolve(timeframe, out var tf))
            throw new ArgumentException($"Invalid timeframe '{timeframe}'. Allowed: 15m, 1h, 4h, 1d, 1w, 1m.", nameof(timeframe));

        var o = _options.CurrentValue;
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-Math.Max(1, o.LookbackDays));

        _logger.LogInformation(
            "OnDemandAnalyzer: fetching {Symbol} [{TimeFrame}] from {From:O} to {To:O}",
            symbol, timeframe, fromUtc, toUtc);

        var bars = await _ohlcvCollector
            .CollectAsync(symbol, fromUtc, toUtc, tf, ct)
            .ConfigureAwait(false);

        if (bars.Count == 0)
        {
            _logger.LogWarning(
                "OnDemandAnalyzer: no OHLCV data for {Symbol} [{TimeFrame}]", symbol, timeframe);
        }

        var news = await FetchNewsAsync(symbol, toUtc, o, ct).ConfigureAwait(false);
        var indicators = ComputeIndicators(bars);
        var market = ResolveMarket(symbol);
        var marketType = BarTimeFrameResolver.GetMarketType(tf);
        var currentPrice = bars.Count > 0 ? (decimal)bars[^1].Close : 0m;
        var regime = MarketRegimeDetector.Detect(indicators, bars.Count > 0 ? bars[^1].Close : 0);

        var request = new AnalysisRequest(symbol, market, marketType, timeframe, bars, indicators, news, regime);
        var signal = await _llm.AnalyzeAsync(request, ct).ConfigureAwait(false);

        var signalResult = _signalEngine.Evaluate(signal, indicators, symbol);
        var risk = _riskCalculator.Calculate(signalResult.Decision, currentPrice, indicators, marketType);
        var marketStructure = ComputeMarketStructure(indicators);

        var result = new AnalysisResult
        {
            Symbol = symbol,
            Market = market,
            MarketType = marketType,
            Timeframe = timeframe,
            CurrentPrice = currentPrice,
            Indicators = indicators,
            MarketStructure = marketStructure,
            Decision = signalResult.Decision,
            Reason = BuildReason(signal, signalResult),
            ExpectedHoldTime = ResolveHoldTimeHint(timeframe),
            RiskManagement = risk,
            NewsSentiment = ResolveNewsSentiment(signal.NewsImpact),
            NewsSummary = "",
            Confidence = signal.Confidence,
            MarketRegime = regime,
            SignalSource = "hybrid",
            AnalyzedAtUtc = DateTime.UtcNow
        };

        _logger.LogInformation(
            "OnDemandAnalyzer: {Symbol} ({TimeFrame}) → Decision={Decision}, Confidence={Confidence:F2}, Regime={Regime}",
            symbol, timeframe, result.Decision, result.Confidence, result.MarketRegime);

        return result;
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchNewsAsync(
        string symbol, DateTime toUtc, CollectorOptions o, CancellationToken ct)
    {
        try
        {
            var fromUtc = toUtc.AddHours(-Math.Max(1, o.NewsLookbackHours));
            return await _newsCollector.CollectAsync(symbol, fromUtc, toUtc, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnDemandAnalyzer: failed to fetch news for {Symbol}, proceeding without", symbol);
            return [];
        }
    }

    private static IndicatorSnapshot ComputeIndicators(IReadOnlyList<OhlcvBar> bars)
    {
        var ema = new EMA();
        var macd = new MACD();
        var rsi = new RSI();
        var bb = new BollingerBands();
        var atr = new ATR();
        var vol = new Volume();
        var vwap = new VWAP();

        TrendCalculator.FillEma(ema, bars);
        TrendCalculator.FillMacd(macd, bars);
        MomentumCalculator.FillRsi(rsi, bars);
        VolatilityCalculator.FillBollingerBands(bb, bars);
        VolatilityCalculator.FillAtr(atr, bars);
        VolumeCalculator.FillVolumeMa(vol, bars);
        VolumeCalculator.FillVwap(vwap, bars);

        return new IndicatorSnapshot
        {
            Ema = ema.Values,
            Macd = macd.Values,
            Rsi = rsi.Values,
            BollingerBands = bb.Values,
            Atr = atr.Values,
            VolumeMa = vol.Values,
            Vwap = vwap.Values
        };
    }

    private static MarketStructure ComputeMarketStructure(IndicatorSnapshot indicators)
    {
        return new MarketStructure
        {
            ShortTermTrend = ResolveEmaTrend(indicators, "9", "21"),
            MidTermTrend = ResolveEmaTrend(indicators, "21", "50"),
            LongTermTrend = ResolveEmaTrend(indicators, "50", "200")
        };
    }

    private static TrendDirection ResolveEmaTrend(IndicatorSnapshot indicators, string fastKey, string slowKey)
    {
        if (!indicators.Ema.TryGetValue(fastKey, out var fast) ||
            !indicators.Ema.TryGetValue(slowKey, out var slow))
            return TrendDirection.Sideways;

        if (fast > slow) return TrendDirection.Uptrend;
        if (fast < slow) return TrendDirection.Downtrend;
        return TrendDirection.Sideways;
    }

    private static string BuildReason(LlmSignal signal, SignalResult signalResult)
    {
        if (signalResult.WasRejected)
            return $"{signal.Reason} [Signal rejected: {signalResult.RejectionReason}]";

        return signal.Reason;
    }

    private static string ResolveNewsSentiment(float newsImpact) => newsImpact switch
    {
        > 0.3f => "positive",
        < -0.3f => "negative",
        0f => "none",
        _ => "neutral"
    };

    private static string ResolveHoldTimeHint(string timeframe) =>
        timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" => "1-4 hours",
            "1h" or "1hour" or "60m" => "4-12 hours",
            "4h" or "4hour" or "240m" => "1-3 days",
            "1d" or "1day" or "d" => "3-14 days",
            "1w" or "1week" or "w" => "2-8 weeks",
            "1m" or "1month" or "mo" => "1-6 months",
            _ => "depends on timeframe"
        };

    private static AssetMarket ResolveMarket(string symbol) =>
        symbol.Contains('/') ? AssetMarket.Crypto : AssetMarket.Stock;
}
