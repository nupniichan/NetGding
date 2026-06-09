using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Persistence;

public sealed class TradingDbContext : DbContext
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options)
    {
    }

    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.ToTable("AnalysisResults");

            // Composite primary key
            entity.HasKey(e => new { e.Symbol, e.Timeframe, e.AnalyzedAtUtc });

            entity.Property(e => e.Symbol).HasMaxLength(50);
            entity.Property(e => e.Timeframe).HasMaxLength(20);
            entity.Ignore(e => e.ChartSymbol);

            // Serialize complex structures to JSON strings for maximum DB portability (SQLite, PG, SQL Server)
            entity.Property(e => e.Indicators)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, s_jsonOptions),
                    v => JsonSerializer.Deserialize<IndicatorSnapshot>(v, s_jsonOptions) ?? new IndicatorSnapshot());

            entity.Property(e => e.MarketStructure)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, s_jsonOptions),
                    v => JsonSerializer.Deserialize<MarketStructure>(v, s_jsonOptions) ?? new MarketStructure());

            entity.Property(e => e.RiskManagement)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, s_jsonOptions),
                    v => JsonSerializer.Deserialize<RiskManagement>(v, s_jsonOptions) ?? new RiskManagement());
        });
    }
}
