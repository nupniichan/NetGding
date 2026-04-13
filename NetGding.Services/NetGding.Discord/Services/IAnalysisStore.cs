using NetGding.Contracts.Models.Analysis;

namespace NetGding.Discord.Services;

public interface IAnalysisStore
{
    void Store(AnalysisResult result);
    AnalysisResult? GetLatest(string symbol);
    IReadOnlyDictionary<string, AnalysisResult> GetAll();
}