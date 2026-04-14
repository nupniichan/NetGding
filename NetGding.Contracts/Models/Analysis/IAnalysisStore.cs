namespace NetGding.Contracts.Models.Analysis;

public interface IAnalysisStore
{
    void Store(AnalysisResult result);
    AnalysisResult? GetLatest(string symbol);
    IReadOnlyDictionary<string, AnalysisResult> GetAll();
}