using Microsoft.AspNetCore.Mvc;
using NetGding.Collector.Services;

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
        OnDemandAnalyzer analyzer,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol) || string.IsNullOrWhiteSpace(request.Timeframe))
            return Results.BadRequest("Symbol and Timeframe are required.");

        try
        {
            var result = await analyzer.AnalyzeAsync(request.Symbol.Trim(), request.Timeframe.Trim(), ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
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

public sealed record OnDemandRequest(string Symbol, string Timeframe);
