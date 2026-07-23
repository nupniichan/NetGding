using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Indicators;
using NetGding.Analyzer.Llm;
using NetGding.Analyzer.Signal;
using NetGding.ChartRenderer;
using NetGding.Configurations.Options;
using NetGding.Collector.Services.MarketData;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.Indicators.Momentum;
using NetGding.Contracts.Models.Indicators.Trends;
using NetGding.Contracts.Models.Indicators.Volatility;
using NetGding.Contracts.Models.Indicators.Volume;

namespace NetGding.Collector.Services;

public sealed class OnDemandAnalyzer : IOnDemandAnalyzer
{
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IMarketDataCollectorResolver _collectorResolver;
    private readonly ILlmAnalyzer _llm;
    private readonly ISignalEngine _signalEngine;
    private readonly IRiskCalculator _riskCalculator;
    private readonly IChartRenderer _chartRenderer;
    private readonly ICachedMarketDataProvider _cachedMarketData;
    private readonly ILogger<OnDemandAnalyzer> _logger;

    public OnDemandAnalyzer(
        IOptionsMonitor<CollectorOptions> options,
        IMarketDataCollectorResolver collectorResolver,
        ILlmAnalyzer llm,
        ISignalEngine signalEngine,
        IRiskCalculator riskCalculator,
        IChartRenderer chartRenderer,
        ICachedMarketDataProvider cachedMarketData,
        ILogger<OnDemandAnalyzer> logger)
    {
        _options = options;
        _collectorResolver = collectorResolver;
        _llm = llm;
        _signalEngine = signalEngine;
        _riskCalculator = riskCalculator;
        _chartRenderer = chartRenderer;
        _cachedMarketData = cachedMarketData;
        _logger = logger;
    }

    public async Task<AnalysisNotification> AnalyzeAsync(
        string symbol,
        string timeframe,
        string exchange,
        string marketType,
        string? chartSymbol = null,
        bool chartOnly = false,
        CancellationToken ct = default)
    {
        if (!TimeframeResolver.TryResolve(timeframe, out _))
            throw new ArgumentException($"Invalid timeframe '{timeframe}'. Allowed: 15m, 1h, 4h, 1d, 1w, 1m.", nameof(timeframe));
        if (!TryResolveMarketType(marketType, out var resolvedMarketType))
            throw new ArgumentException($"Invalid market type '{marketType}'. Allowed: spot, future.", nameof(marketType));

        var bars = await FetchMarketBarsAsync(symbol, timeframe, exchange, resolvedMarketType, ct).ConfigureAwait(false);
        var market = ResolveMarket(symbol);
        var indicators = ComputeIndicators(bars, timeframe);
        var currentPrice = (decimal)bars[^1].Close;
        var regime = MarketRegimeDetector.Detect(indicators, bars[^1].Close);

        bool isChartRequest = chartOnly || !string.IsNullOrWhiteSpace(chartSymbol);
        var (signal, signalResult, risk, marketStructure, fngIndex, fngClass) = isChartRequest
            ? (null, null, null, null, null, null)
            : await ExecuteFullAnalysisAsync(symbol, timeframe, resolvedMarketType, market, bars, indicators, currentPrice, regime, ct).ConfigureAwait(false);

        var result = new AnalysisResult
        {
            Symbol = symbol,
            ChartSymbol = chartSymbol,
            Market = market,
            MarketType = resolvedMarketType,
            Timeframe = timeframe,
            CurrentPrice = currentPrice,
            Indicators = indicators,
            MarketStructure = marketStructure ?? new MarketStructure(),
            Decision = signalResult?.Decision ?? TradeDecision.Wait,
            Reason = (signal != null && signalResult != null) ? BuildReason(signal, signalResult) : "Chart Only",
            ExpectedHoldTime = ResolveHoldTimeHint(timeframe),
            RiskManagement = risk ?? new RiskManagement(),
            NewsSentiment = signal != null ? ResolveNewsSentiment(signal.NewsImpact) : "none",
            NewsSummary = "",
            Confidence = signal?.Confidence ?? 0f,
            MarketRegime = regime,
            SignalSource = "hybrid",
            FearAndGreedIndex = fngIndex,
            FearAndGreedClassification = fngClass,
            AnalyzedAtUtc = DateTime.UtcNow
        };

        _logger.LogInformation(
            "OnDemandAnalyzer: {Symbol} ({TimeFrame}) → Decision={Decision}, Confidence={Confidence:F2}, Regime={Regime}",
            symbol, timeframe, result.Decision, result.Confidence, result.MarketRegime);

        var notification = new AnalysisNotification { Result = result };
        await RenderChartIfEnabledAsync(notification, bars, result, exchange, isChartRequest, ct).ConfigureAwait(false);
        return notification;
    }

    private async Task<IReadOnlyList<OhlcvBar>> FetchMarketBarsAsync(
        string symbol, string timeframe, string exchange, MarketType marketType, CancellationToken ct)
    {
        var o = _options.CurrentValue;
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-ComputeLookbackDays(timeframe, o.LookbackDays));

        _logger.LogInformation(
            "OnDemandAnalyzer: fetching {Symbol} [{TimeFrame}, {Exchange}, {MarketType}] from {From:O} to {To:O}",
            symbol, timeframe, exchange, marketType, fromUtc, toUtc);

        var collector = _collectorResolver.Resolve(exchange, marketType);
        if (collector is null)
        {
            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.CollectorNotFound,
                "OnDemandAnalyzer.FetchMarketBarsAsync",
                $"No collector resolved for exchange '{exchange}' and market type '{marketType}'.");
        }

        var bars = await collector.CollectAsync(symbol, fromUtc, toUtc, timeframe, ct).ConfigureAwait(false);
        if (bars.Count == 0)
        {
            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.NoMarketData,
                "OnDemandAnalyzer.FetchMarketBarsAsync",
                $"No market data (OHLCV) found for {symbol} on {exchange} [{timeframe}].");
        }

        return bars;
    }

    private async Task<(LlmSignal? Signal, SignalResult SignalResult, RiskManagement Risk, MarketStructure Structure, int? FngIndex, string? FngClass)>
        ExecuteFullAnalysisAsync(
            string symbol, string timeframe, MarketType marketType, AssetMarket market,
            IReadOnlyList<OhlcvBar> bars, IndicatorSnapshot indicators, decimal currentPrice, MarketRegime regime, CancellationToken ct)
    {
        int? fngIndex = null;
        string? fngClass = null;

        var news = await _cachedMarketData.GetNewsAsync(symbol, ct).ConfigureAwait(false);
        if (market == AssetMarket.Crypto)
        {
            var fng = await _cachedMarketData.GetFearAndGreedAsync(ct).ConfigureAwait(false);
            if (fng != null)
            {
                fngIndex = fng.Value;
                fngClass = fng.Classification;
            }
        }

        var request = new AnalysisRequest(
            symbol, market, marketType, timeframe, bars, indicators, news, regime,
            fngIndex, fngClass);

        var signal = await _llm.AnalyzeAsync(request, ct).ConfigureAwait(false);
        var signalResult = _signalEngine.Evaluate(signal, indicators, symbol, regime);
        var risk = _riskCalculator.Calculate(signalResult.Decision, currentPrice, indicators, marketType);
        var marketStructure = ComputeMarketStructure(indicators);

        return (signal, signalResult, risk, marketStructure, fngIndex, fngClass);
    }

    private async Task RenderChartIfEnabledAsync(
        AnalysisNotification notification, IReadOnlyList<OhlcvBar> bars, AnalysisResult result,
        string exchange, bool isChartRequest, CancellationToken ct)
    {
        if (!_options.CurrentValue.ChartEnabled || bars.Count == 0)
            return;

        try
        {
            var chartBytes = await _chartRenderer.RenderAsync(bars, result, exchange, ct).ConfigureAwait(false);
            if (chartBytes.Length > 0)
            {
                notification.ChartImageBase64 = Convert.ToBase64String(chartBytes);
            }
            else
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    ErrorCodes.ChartRenderFailed,
                    "OnDemandAnalyzer.RenderChartIfEnabledAsync",
                    "Chart rendering completed but returned empty bytes.");
            }
        }
        catch (Exception ex) when (ex is not NetGding.Contracts.Exceptions.NetGdingException)
        {
            throw new NetGding.Contracts.Exceptions.NetGdingException(
                ErrorCodes.ChartRenderFailed,
                "OnDemandAnalyzer.RenderChartIfEnabledAsync",
                $"Chart rendering failed: {ex.Message}", ex);
        }
    }

    private static bool TryResolveMarketType(string requested, out MarketType marketType) =>
        MarketParsingHelper.TryResolveMarketType(requested, out marketType);

    private static IndicatorSnapshot ComputeIndicators(IReadOnlyList<OhlcvBar> bars, string timeframe)
    {
        var tfGroup = ResolveTimeframeGroup(timeframe);

        var ema = new EMA();
        var macd = new MACD();
        var rsi = new RSI();
        var bb = new BollingerBands();
        var atr = new ATR();
        var vol = new Volume();
        var vwap = new VWAP();

        TrendCalculator.FillEmaFiltered(ema, bars, GetEmaPeriods(tfGroup));
        TrendCalculator.FillMacd(macd, bars);
        MomentumCalculator.FillRsi(rsi, bars);
        VolatilityCalculator.FillBollingerBands(bb, bars);
        VolatilityCalculator.FillAtr(atr, bars);

        if (tfGroup != TimeframeGroup.Position)
            VolumeCalculator.FillVolumeMa(vol, bars);

        if (tfGroup == TimeframeGroup.Intraday)
            VolumeCalculator.FillVwap(vwap, bars);

        var snapshot = new IndicatorSnapshot
        {
            Ema = ema.Values,
            Macd = macd.Values,
            Rsi = rsi.Values,
            BollingerBands = bb.Values,
            Atr = atr.Values,
            VolumeMa = vol.Values,
            Vwap = vwap.Values
        };

        var atrValue = atr.Values.Count > 0 ? (double)atr.Values.Values.Max() : 0;
        SupportResistanceCalculator.Fill(snapshot, bars, timeframe, atrValue);

        return snapshot;
    }

    private static TimeframeGroup ResolveTimeframeGroup(string timeframe) =>
        timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" or "1h" or "1hour" or "4h" or "4hour" => TimeframeGroup.Intraday,
            "1d" or "1day" or "d" => TimeframeGroup.Swing,
            _ => TimeframeGroup.Position
        };

    private static IEnumerable<int> GetEmaPeriods(TimeframeGroup group) => group switch
    {
        TimeframeGroup.Intraday => [9, 21, 50],
        TimeframeGroup.Swing    => [9, 21, 50, 100, 200],
        _                       => [21, 50, 100, 200]
    };

    private static int ComputeLookbackDays(string timeframe, int configuredMin)
    {
        const int minBars = 250;
        const int maxLookbackDays = 3650;
        var days = timeframe.ToLowerInvariant() switch
        {
            "15m" or "15min" => (int)Math.Ceiling(minBars * 15.0 / 1440) + 2,
            "1h" or "1hour"  => (int)Math.Ceiling(minBars / 24.0) + 2,
            "4h" or "4hour"  => (int)Math.Ceiling(minBars * 4.0 / 24.0) + 5,
            "1d" or "1day"   => minBars + 30,
            "1w" or "1week"  => minBars * 7 + 30,
            "1m" or "1month" => minBars * 31,
            _ => 30
        };
        return Math.Min(Math.Max(days, configuredMin), maxLookbackDays);
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