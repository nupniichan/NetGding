using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.News;

namespace NetGding.Collector.Services.MarketData;

public interface ICachedMarketDataProvider
{
    Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default);
    Task<FearAndGreedResult?> GetFearAndGreedAsync(CancellationToken ct = default);
    
    // Temporary execution cache per request
    void CacheTemporaryStep<T>(string requestId, string stepKey, T value);
    T? GetTemporaryStep<T>(string requestId, string stepKey);
    void ClearTemporarySteps(string requestId);
}
