using Microsoft.AspNetCore.Mvc;
using NetGding.Collector.Services;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Collector.Endpoints;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analysis/on-demand", HandleOnDemandAsync)
           .WithName("OnDemandAnalysis")
           .WithTags("Analysis");
    }

    private static async Task<IResult> HandleOnDemandAsync(
        [FromBody] OnDemandRequest request,
        IOnDemandAnalyzer analyzer,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol) ||
            string.IsNullOrWhiteSpace(request.Timeframe) ||
            string.IsNullOrWhiteSpace(request.Exchange) ||
            string.IsNullOrWhiteSpace(request.MarketType))
        {
            return Results.BadRequest("Symbol, Timeframe, Exchange, and MarketType are required.");
        }

        try
        {
            var notification = await analyzer.AnalyzeAsync(
                    request.Symbol.Trim(),
                    request.Timeframe.Trim().ToLowerInvariant(),
                    request.Exchange.Trim().ToLowerInvariant(),
                    request.MarketType.Trim().ToLowerInvariant(),
                    ct)
                .ConfigureAwait(false);

            return Results.Ok(notification);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "On-demand analysis failed for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                request.Symbol, request.Timeframe, request.Exchange, request.MarketType);
            return Results.StatusCode(500);
        }
    }
}
