using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface IAnalysisResultStore
{
    void Store(AnalysisResult result);
    AnalysisResult? GetLatest(string symbol, string timeframe);
    IReadOnlyList<AnalysisResult> GetHistory(
        string symbol,
        string timeframe,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize);
}
