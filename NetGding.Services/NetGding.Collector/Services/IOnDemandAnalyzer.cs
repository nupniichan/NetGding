using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public interface IOnDemandAnalyzer
{
    Task<AnalysisNotification> AnalyzeAsync(
        string symbol,
        string timeframe,
        string exchange,
        string marketType,
        string? chartSymbol = null,
        bool chartOnly = false,
        CancellationToken ct = default);
}
