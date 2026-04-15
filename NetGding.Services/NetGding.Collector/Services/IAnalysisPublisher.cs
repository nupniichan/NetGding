using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Services;

public interface IAnalysisPublisher
{
    Task PublishAsync(AnalysisNotification notification, CancellationToken ct = default);
}
