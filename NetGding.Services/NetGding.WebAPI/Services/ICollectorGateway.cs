using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface ICollectorGateway
{
    Task<AnalysisResult?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default);
}
