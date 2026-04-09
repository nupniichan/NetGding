using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;

namespace NetGding.Analyzer.Llm;

public sealed record AnalysisRequest(
    string Symbol,
    AssetMarket Market,
    MarketType MarketType,
    string Timeframe,
    IReadOnlyList<OhlcvBar> Bars,
    IndicatorSnapshot Indicators,
    IReadOnlyList<NewsArticle> News);

public interface ILlmAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default);
}
