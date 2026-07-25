using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;
using NetGding.Contracts.Models.News;

namespace NetGding.Contracts.Services;

public interface IWebApiClient
{
    Task<AnalysisNotification> FetchOnDemandAnalysisAsync(
        OnDemandRequest request,
        int maxRetries = 3,
        int retryBaseDelaySeconds = 2,
        CancellationToken ct = default);

    Task<IReadOnlyList<NewsItem>> FetchNewsAsync(
        string symbol,
        int limit = 5,
        CancellationToken ct = default);

    Task<FearAndGreedResult> FetchFearAndGreedAsync(CancellationToken ct = default);

    Task<MarketDepthDto?> FetchDomAsync(
        string symbol,
        string exchange,
        string marketType,
        int limit = 10,
        CancellationToken ct = default);
}
