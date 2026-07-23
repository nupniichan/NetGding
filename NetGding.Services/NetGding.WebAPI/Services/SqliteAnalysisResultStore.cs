using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Persistence;

namespace NetGding.WebApi.Services;

public sealed class SqliteAnalysisResultStore : IAnalysisResultStore
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<SqliteAnalysisResultStore> _logger;

    public SqliteAnalysisResultStore(
        TradingDbContext dbContext,
        ILogger<SqliteAnalysisResultStore> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StoreAsync(AnalysisResult result, CancellationToken ct = default)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Symbol))
            return;

        try
        {
            var exists = await _dbContext.AnalysisResults.AnyAsync(x => 
                x.Symbol == result.Symbol && 
                x.Timeframe == result.Timeframe && 
                x.AnalyzedAtUtc == result.AnalyzedAtUtc, ct).ConfigureAwait(false);

            if (!exists)
            {
                await _dbContext.AnalysisResults.AddAsync(result, ct).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("[SqliteAnalysisResultStore] Successfully stored analysis result for {Symbol} ({Timeframe})", result.Symbol, result.Timeframe);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SqliteAnalysisResultStore] Error storing analysis result for {Symbol} ({Timeframe})", result.Symbol, result.Timeframe);
        }
    }

    public AnalysisResult? GetLatest(string symbol, string timeframe)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
            return null;

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedTf = timeframe.Trim().ToUpperInvariant();

        return _dbContext.AnalysisResults
            .Where(x => x.Symbol.ToUpper() == normalizedSymbol && x.Timeframe.ToUpper() == normalizedTf)
            .OrderByDescending(x => x.AnalyzedAtUtc)
            .FirstOrDefault();
    }

    public IReadOnlyList<AnalysisResult> GetHistory(
        string symbol,
        string timeframe,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
            return [];

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedTf = timeframe.Trim().ToUpperInvariant();

        var query = _dbContext.AnalysisResults
            .Where(x => x.Symbol.ToUpper() == normalizedSymbol && x.Timeframe.ToUpper() == normalizedTf);

        if (fromUtc.HasValue)
            query = query.Where(x => x.AnalyzedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.AnalyzedAtUtc <= toUtc.Value);

        return query
            .OrderByDescending(x => x.AnalyzedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }
}
