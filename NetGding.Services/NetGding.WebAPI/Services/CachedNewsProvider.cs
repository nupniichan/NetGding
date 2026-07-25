using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class CachedNewsProvider : INewsProvider
{
    private readonly CompositeNewsProvider _innerProvider;
    private readonly INewsCacheStore _cacheStore;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<CachedNewsProvider> _logger;

    public CachedNewsProvider(
        CompositeNewsProvider innerProvider,
        INewsCacheStore cacheStore,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<CachedNewsProvider> logger)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        var refreshHours = opts.NewsCacheRefreshHours > 0 ? opts.NewsCacheRefreshHours : 6;
        var retentionDays = opts.NewsRetentionDays > 0 ? opts.NewsRetentionDays : 5;

        // 1. Try reading from DB cache first
        try
        {
            var cached = await _cacheStore
                .GetCachedNewsAsync(symbol, limit, fromUtc, toUtc, refreshHours, ct)
                .ConfigureAwait(false);

            if (cached != null)
            {
                _logger.LogInformation("CachedNewsProvider: Serving news for {Symbol} from SQLite DB.", symbol);
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CachedNewsProvider: Error checking DB cache for {Symbol}, falling back to remote fetch.", symbol);
        }

        // 2. Cache miss or stale -> Call external news providers
        _logger.LogInformation("CachedNewsProvider: Fetching fresh news for {Symbol} from external APIs.", symbol);
        var freshNews = await _innerProvider
            .GetNewsAsync(symbol, limit, fromUtc, toUtc, ct)
            .ConfigureAwait(false);

        // 3. Store fresh news in DB cache & trigger expired news cleanup
        if (freshNews.Count > 0)
        {
            await _cacheStore.StoreNewsAsync(symbol, freshNews, ct).ConfigureAwait(false);
        }

        await _cacheStore.CleanupExpiredNewsAsync(retentionDays, ct).ConfigureAwait(false);

        return freshNews;
    }
}
