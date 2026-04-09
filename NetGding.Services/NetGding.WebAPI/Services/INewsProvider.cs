using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public interface INewsProvider
{
    Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);
}
