using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public interface INewsCacheStore
{
    Task<IReadOnlyList<NewsItemDto>?> GetCachedNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        int refreshHours,
        CancellationToken ct = default);

    Task StoreNewsAsync(
        string symbol,
        IReadOnlyList<NewsItemDto> items,
        CancellationToken ct = default);

    Task CleanupExpiredNewsAsync(
        int retentionDays,
        CancellationToken ct = default);
}
