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
        if (string.IsNullOrWhiteSpace(request.Symbol) || string.IsNullOrWhiteSpace(request.Timeframe))
            return Results.BadRequest("Symbol and Timeframe are required.");

        try
        {
            var notification = await analyzer.AnalyzeAsync(request.Symbol.Trim(), request.Timeframe.Trim(), ct)
                .ConfigureAwait(false);

            return Results.Ok(notification);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "On-demand analysis failed for {Symbol} ({Timeframe})",
                request.Symbol, request.Timeframe);
            return Results.StatusCode(500);
        }
    }
}
