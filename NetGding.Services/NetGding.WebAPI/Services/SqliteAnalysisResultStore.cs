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

    public void Store(AnalysisResult result)
    {
        var exists = _dbContext.AnalysisResults.Any(x => 
            x.Symbol == result.Symbol && 
            x.Timeframe == result.Timeframe && 
            x.AnalyzedAtUtc == result.AnalyzedAtUtc);

        if (!exists)
        {
            _dbContext.AnalysisResults.Add(result);
            _dbContext.SaveChanges();
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
