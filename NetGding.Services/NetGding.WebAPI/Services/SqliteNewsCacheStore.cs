using Microsoft.EntityFrameworkCore;
using NetGding.WebApi.Models;
using NetGding.WebApi.Persistence;

namespace NetGding.WebApi.Services;

public sealed class SqliteNewsCacheStore : INewsCacheStore
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<SqliteNewsCacheStore> _logger;

    public SqliteNewsCacheStore(
        TradingDbContext dbContext,
        ILogger<SqliteNewsCacheStore> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<NewsItemDto>?> GetCachedNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        int refreshHours,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return [];

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var latestBoundaryUtc = GetLatestRefreshBoundaryUtc(DateTime.UtcNow, refreshHours);

        var latestFetched = await _dbContext.CachedNewsItems
            .AsNoTracking()
            .Where(x => x.Symbol == normalizedSymbol)
            .MaxAsync(x => (DateTime?)x.FetchedAtUtc, ct)
            .ConfigureAwait(false);

        if (!latestFetched.HasValue || latestFetched.Value < latestBoundaryUtc)
        {
            _logger.LogInformation(
                "[SqliteNewsCacheStore] Cache miss or stale for {Symbol}. LatestFetched={LatestFetched}, Boundary={Boundary}",
                normalizedSymbol, latestFetched, latestBoundaryUtc);
            return null;
        }

        _logger.LogInformation(
            "[SqliteNewsCacheStore] Cache hit for {Symbol}. Data fetched at {LatestFetched} (after boundary {Boundary})",
            normalizedSymbol, latestFetched, latestBoundaryUtc);

        var query = _dbContext.CachedNewsItems
            .AsNoTracking()
            .Where(x => x.Symbol == normalizedSymbol);

        if (fromUtc.HasValue)
            query = query.Where(x => x.PublishedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.PublishedAtUtc <= toUtc.Value);

        var entities = await query
            .OrderByDescending(x => x.PublishedAtUtc)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => new NewsItemDto(
            e.Id,
            e.Symbol,
            e.Title,
            e.Source,
            e.Url,
            e.PublishedAtUtc,
            e.Summary,
            e.Sentiment
        )).ToList();
    }

    public async Task StoreNewsAsync(
        string symbol,
        IReadOnlyList<NewsItemDto> items,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol) || items.Count == 0)
            return;

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var nowUtc = DateTime.UtcNow;

        try
        {
            foreach (var item in items)
            {
                var id = item.Id != 0 ? item.Id : GenerateIdFromUrl(item.Url);
                var existing = await _dbContext.CachedNewsItems
                    .FindAsync([id, normalizedSymbol], ct)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    existing.Title = item.Title;
                    existing.Source = item.Source;
                    existing.Url = item.Url;
                    existing.PublishedAtUtc = item.PublishedAtUtc;
                    existing.Summary = item.Summary;
                    existing.Sentiment = item.Sentiment;
                    existing.FetchedAtUtc = nowUtc;
                }
                else
                {
                    await _dbContext.CachedNewsItems.AddAsync(new NewsItemEntity
                    {
                        Id = id,
                        Symbol = normalizedSymbol,
                        Title = item.Title,
                        Source = item.Source,
                        Url = item.Url,
                        PublishedAtUtc = item.PublishedAtUtc,
                        Summary = item.Summary,
                        Sentiment = item.Sentiment,
                        FetchedAtUtc = nowUtc
                    }, ct).ConfigureAwait(false);
                }
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "[SqliteNewsCacheStore] Successfully stored {Count} news items for {Symbol} into DB",
                items.Count, normalizedSymbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SqliteNewsCacheStore] Error storing news items for {Symbol}", normalizedSymbol);
        }
    }

    public async Task CleanupExpiredNewsAsync(
        int retentionDays,
        CancellationToken ct = default)
    {
        if (retentionDays <= 0)
            retentionDays = 5;

        try
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
            var expired = await _dbContext.CachedNewsItems
                .Where(x => x.PublishedAtUtc < cutoffUtc && x.FetchedAtUtc < cutoffUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (expired.Count > 0)
            {
                _dbContext.CachedNewsItems.RemoveRange(expired);
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("[SqliteNewsCacheStore] Cleaned up {Count} expired news items older than {Cutoff}",
                    expired.Count, cutoffUtc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SqliteNewsCacheStore] Error cleaning up expired news");
        }
    }

    internal static DateTime GetLatestRefreshBoundaryUtc(DateTime nowUtc, int refreshHours)
    {
        if (refreshHours <= 0 || refreshHours > 24)
            refreshHours = 6;

        var currentWindowStartHour = (nowUtc.Hour / refreshHours) * refreshHours;
        return new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, currentWindowStartHour, 0, 0, DateTimeKind.Utc);
    }

    private static long GenerateIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;
        ulong hash = 14695981039346656037UL;
        foreach (char c in url)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return (long)hash;
    }
}
