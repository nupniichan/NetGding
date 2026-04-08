using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public interface IOnDemandAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(string symbol, string timeframe, CancellationToken ct = default);
}
