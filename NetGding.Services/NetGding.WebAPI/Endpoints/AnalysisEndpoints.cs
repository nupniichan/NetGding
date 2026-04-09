using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Endpoints;

public static class AnalysisEndpoints
{
    public static void MapAnalysisEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analysis/on-demand", HandleOnDemandAsync)
           .WithName("OnDemandAnalysis")
           .WithTags("Analysis");

        app.MapPost("/api/analysis/publish", HandlePublishAsync)
           .WithName("PublishAnalysis")
           .WithTags("Analysis");

        app.MapGet("/api/analysis/latest/{symbol}", HandleGetLatestAsync)
           .WithName("GetLatestAnalysis")
           .WithTags("Analysis");

        app.MapGet("/api/analysis/history/{symbol}", HandleGetHistoryAsync)
           .WithName("GetAnalysisHistory")
           .WithTags("Analysis");
    }

    private static async Task<IResult> HandleOnDemandAsync(
        [FromBody] OnDemandRequest request,
        ICollectorGateway collectorGateway,
        IAnalysisResultStore analysisResultStore,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol) || string.IsNullOrWhiteSpace(request.Timeframe))
            return Results.BadRequest("Symbol and Timeframe are required.");

        var normalizedRequest = new OnDemandRequest(request.Symbol.Trim(), request.Timeframe.Trim());
        var result = await collectorGateway.AnalyzeOnDemandAsync(normalizedRequest, ct).ConfigureAwait(false);
        if (result is null)
        {
            logger.LogError("On-demand proxy failed for {Symbol} ({Timeframe})",
                normalizedRequest.Symbol, normalizedRequest.Timeframe);
            return Results.StatusCode(502);
        }

        analysisResultStore.Store(result);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandlePublishAsync(
        [FromBody] AnalysisResult result,
        ITelegramForwarder forwarder,
        IAnalysisResultStore analysisResultStore,
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
            analysisResultStore.Store(result);
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

    private static IResult HandleGetLatestAsync(
        [FromRoute] string symbol,
        [FromQuery] string timeframe,
        IAnalysisResultStore analysisResultStore)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
            return Results.BadRequest("Symbol and timeframe are required.");

        var latest = analysisResultStore.GetLatest(symbol, timeframe);
        return latest is null ? Results.NotFound() : Results.Ok(latest);
    }

    private static IResult HandleGetHistoryAsync(
        [FromRoute] string symbol,
        [FromQuery] string timeframe,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IAnalysisResultStore analysisResultStore)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
            return Results.BadRequest("Symbol and timeframe are required.");

        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

        var items = analysisResultStore.GetHistory(
            symbol,
            timeframe,
            from,
            to,
            normalizedPage,
            normalizedPageSize);

        return Results.Ok(new
        {
            Symbol = symbol.Trim(),
            Timeframe = timeframe.Trim(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            Count = items.Count,
            Items = items
        });
    }
}