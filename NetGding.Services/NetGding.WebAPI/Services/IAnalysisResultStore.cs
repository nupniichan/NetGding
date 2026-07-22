using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface IAnalysisResultStore
{
    Task StoreAsync(AnalysisResult result, CancellationToken ct = default);
    AnalysisResult? GetLatest(string symbol, string timeframe);
    IReadOnlyList<AnalysisResult> GetHistory(
        string symbol,
        string timeframe,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize);
}

