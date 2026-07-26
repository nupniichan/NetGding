using NetGding.Contracts.Models.Analysis;

namespace NetGding.Contracts.Services;

public interface IAnalysisCache
{
    void Store(AnalysisResult result);
    AnalysisResult? GetLatest(string symbol, string? timeframe = null);
    IReadOnlyDictionary<string, AnalysisResult> GetAll();
}
