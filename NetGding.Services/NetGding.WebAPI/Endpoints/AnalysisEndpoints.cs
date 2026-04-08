using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Endpoints;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analysis/publish", HandlePublishAsync)
           .WithName("PublishAnalysis")
           .WithTags("Analysis");
    }

    private static async Task<IResult> HandlePublishAsync(
        [FromBody] AnalysisResult result,
        ITelegramForwarder forwarder,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.Symbol) ||
            string.IsNullOrWhiteSpace(result.Timeframe) ||
            result.AnalyzedAtUtc == default)
        {
            return Results.BadRequest("Symbol, Timeframe, and AnalyzedAtUtc are required.");
        }

        try
        {
            await forwarder.ForwardAsync(result, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Analysis published for {Symbol} ({Timeframe}) → Decision={Decision}",
                result.Symbol, result.Timeframe, result.Decision);

            return Results.Ok(new { result.Symbol, result.Timeframe, result.Decision, Published = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward analysis for {Symbol}", result.Symbol);
            return Results.StatusCode(502);
        }
    }
}