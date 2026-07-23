using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class CompositeNewsProvider : INewsProvider
{
    private readonly AlphaVantageNewsProvider _primary;
    private readonly GoogleNewsRssNewsProvider _fallback;
    private readonly ILogger<CompositeNewsProvider> _logger;

    public CompositeNewsProvider(
        AlphaVantageNewsProvider primary,
        GoogleNewsRssNewsProvider fallback,
        ILogger<CompositeNewsProvider> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        try
        {
            var primaryResults = await _primary.GetNewsAsync(symbol, limit, fromUtc, toUtc, ct).ConfigureAwait(false);
            if (primaryResults.Count > 0)
                return primaryResults;

            _logger.LogInformation("CompositeNewsProvider: Primary provider returned 0 items for {Symbol}. Falling back to Google News RSS.", symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CompositeNewsProvider: Primary news provider failed for {Symbol}. Falling back to Google News RSS.", symbol);
        }

        return await _fallback.GetNewsAsync(symbol, limit, fromUtc, toUtc, ct).ConfigureAwait(false);
    }
}
