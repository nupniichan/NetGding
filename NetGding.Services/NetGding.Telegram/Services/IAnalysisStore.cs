using NetGding.Contracts.Models.Analysis;

namespace NetGding.Telegram.Services;

public interface IAnalysisStore
{
    void Store(AnalysisResult result);
    AnalysisResult? GetLatest(string symbol);
    IReadOnlyDictionary<string, AnalysisResult> GetAll();
}