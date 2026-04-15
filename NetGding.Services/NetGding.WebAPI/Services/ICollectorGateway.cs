using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface ICollectorGateway
{
    Task<AnalysisNotification?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default);
}
