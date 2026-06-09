using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;

namespace NetGding.Analyzer.Llm;

public interface ILlmAnalyzer
{
    Task<LlmSignal> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default);
}