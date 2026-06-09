using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.WebApi.Services;

public interface ICollectorGateway
{
    Task<AnalysisNotification?> AnalyzeOnDemandAsync(OnDemandRequest request, CancellationToken ct = default);
    Task<MarketDepthDto?> GetDepthAsync(string symbol, string exchange, string marketType, int limit, CancellationToken ct = default);
}
