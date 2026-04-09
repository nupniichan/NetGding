using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Llm;
using NetGding.Analyzer.Indicators;
using NetGding.Collector.Alpaca;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.News;
using NetGding.Models.Indicators.Momentum;
using NetGding.Models.Indicators.Trends;
using NetGding.Models.Indicators.Volatility;
using NetGding.Models.Indicators.Volume;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Services;

public sealed class OnDemandAnalyzer : IOnDemandAnalyzer
{
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IAlpacaOhlcvCollector _ohlcvCollector;
    private readonly IAlpacaNewsCollector _newsCollector;
    private readonly ILlmAnalyzer _llm;
    private readonly ILogger<OnDemandAnalyzer> _logger;

    public OnDemandAnalyzer(
        IOptionsMonitor<CollectorOptions> options,
        IAlpacaOhlcvCollector ohlcvCollector,
        IAlpacaNewsCollector newsCollector,
        ILlmAnalyzer llm,
        ILogger<OnDemandAnalyzer> logger)
    {
        _options = options;
        _ohlcvCollector = ohlcvCollector;
        _newsCollector = newsCollector;
        _llm = llm;
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

        var request = new AnalysisRequest(symbol, market, marketType, timeframe, bars, indicators, news);
        var result = await _llm.AnalyzeAsync(request, ct).ConfigureAwait(false);
        result.AnalyzedAtUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "OnDemandAnalyzer: {Symbol} ({TimeFrame}) → Decision={Decision}",
            symbol, timeframe, result.Decision);

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

    private static AssetMarket ResolveMarket(string symbol) =>
        symbol.Contains('/') ? AssetMarket.Crypto : AssetMarket.Stock;
}