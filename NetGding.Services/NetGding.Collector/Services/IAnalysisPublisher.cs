using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public interface IAnalysisPublisher
{
    Task PublishAsync(AnalysisResult result, CancellationToken ct = default);
}