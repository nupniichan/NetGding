using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;
using NetGding.Models.MarketData;
using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Analyzer.Gemma;

public sealed record AnalysisRequest(
    string Symbol,
    AssetMarket Market,
    MarketType MarketType,
    string Timeframe,
    IReadOnlyList<OhlcvBar> Bars,
    IndicatorSnapshot Indicators,
    IReadOnlyList<NewsArticle> News);

public interface IGemmaAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default);
}