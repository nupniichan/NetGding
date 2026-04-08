using AlpacaBarTimeFrame = Alpaca.Markets.BarTimeFrame;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Gemma;
using NetGding.Analyzer.Indicators;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Configuration;
using NetGding.Collector.Persistence;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.News;
using NetGding.Models.Indicators.Momentum;
using NetGding.Models.Indicators.Trends;
using NetGding.Models.Indicators.Volatility;
using NetGding.Models.Indicators.Volume;
using NetGding.Models.MarketData;

namespace NetGding.Collector.Workers;

public sealed class AnalysisWorker : BackgroundService
{
    private static readonly TimeSpan CollectionOffset = TimeSpan.FromSeconds(30);

    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly IGemmaAnalyzer _gemma;
    private readonly ILogger<AnalysisWorker> _logger;

    public AnalysisWorker(
        IOptionsMonitor<CollectorOptions> options,
        IGemmaAnalyzer gemma,
        ILogger<AnalysisWorker> logger)
    {
        _options = options;
        _gemma = gemma;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(CollectionOffset, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;

            if (!o.AnalysisEnabled)
            {
                _logger.LogDebug("AI analysis is disabled.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (string.IsNullOrWhiteSpace(o.GemmaApiKey) &&
                o.GemmaBaseUrl.Contains("openrouter", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("AnalysisWorker: GemmaApiKey is required for OpenRouter.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (!BarTimeFrameResolver.TryResolve(o.BarTimeFrame, out var tf))
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var boundaryWait = BarTimeFrameResolver.DelayUntilNextBarBoundaryUtc(tf, DateTime.UtcNow);
            await Task.Delay(boundaryWait + CollectionOffset, stoppingToken).ConfigureAwait(false);

            var symbols = o.Symbols ?? [];

            foreach (var raw in symbols)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var symbol = raw.Trim();

                try
                {
                    await AnalyzeSymbolAsync(symbol, o, tf, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AnalysisWorker: failed for {Symbol}", symbol);
                }
            }
        }
    }

    private async Task AnalyzeSymbolAsync(
        string symbol,
        CollectorOptions o,
        AlpacaBarTimeFrame tf,
        CancellationToken ct)
    {
        var series = await JsonLoader.LoadLatestStructAsync<OhlcvSeries>(
            o.OutputDirectory, symbol, "ohlcv", _logger).ConfigureAwait(false);

        if (series is not { } s || s.Bars.Count == 0)
        {
            _logger.LogWarning("AnalysisWorker: no OHLCV data for {Symbol}, skipping", symbol);
            return;
        }

        var bars = s.Bars;
        var indicators = ComputeIndicators(bars);
        var news = await LoadNewsAsync(o.OutputDirectory, symbol).ConfigureAwait(false);
        var market = ResolveMarket(symbol);
        var marketType = BarTimeFrameResolver.GetMarketType(tf);

        var request = new AnalysisRequest(
            symbol, market, marketType, o.BarTimeFrame,
            bars, indicators, news);

        var result = await _gemma.AnalyzeAsync(request, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "AnalysisWorker: {Symbol} ({Timeframe}) → Decision={Decision}",
            symbol, o.BarTimeFrame, result.Decision);

        await JsonPersistence.SaveAsync(
            o.OutputDirectory, symbol, "analysis", result, _logger).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<NewsArticle>> LoadNewsAsync(string outputDir, string symbol)
    {
        var collection = await JsonLoader.LoadLatestAsync<NewsCollection>(
            outputDir, symbol, "news", _logger).ConfigureAwait(false);

        return collection?.Articles ?? [];
    }

    private static AssetMarket ResolveMarket(string symbol)
    {
        if (symbol.Contains('/'))
            return AssetMarket.Crypto;

        return AssetMarket.Stock;
    }
}