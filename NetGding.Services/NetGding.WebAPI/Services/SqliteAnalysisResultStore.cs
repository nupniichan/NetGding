using Microsoft.EntityFrameworkCore;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Persistence;

namespace NetGding.WebApi.Services;

public sealed class SqliteAnalysisResultStore : IAnalysisResultStore
{
    private readonly TradingDbContext _dbContext;

    public SqliteAnalysisResultStore(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task StoreAsync(AnalysisResult result, CancellationToken ct = default)
    {
        var exists = await _dbContext.AnalysisResults.AnyAsync(x => 
            x.Symbol == result.Symbol && 
            x.Timeframe == result.Timeframe && 
            x.AnalyzedAtUtc == result.AnalyzedAtUtc, ct).ConfigureAwait(false);

        if (!exists)
        {
            await _dbContext.AnalysisResults.AddAsync(result, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public AnalysisResult? GetLatest(string symbol, string timeframe)
    {
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
