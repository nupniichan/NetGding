using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Models;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Endpoints;

public static class IndicatorEndpoints
{
    public static void MapIndicatorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/indicators/{*symbol}", HandleGetIndicatorsAsync)
           .WithName("GetIndicatorsBySymbol")
           .WithTags("Indicators");
    }

    private static async Task<IResult> HandleGetIndicatorsAsync(
        [FromRoute] string symbol,
        [FromQuery] string timeframe,
        [FromQuery] string? exchange,
        [FromQuery] string? marketType,
        [FromQuery] bool detail,
        ICollectorGateway collectorGateway,
        IAnalysisResultStore analysisResultStore,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
        {
            return Results.BadRequest("Symbol and timeframe are required.");
        }

        var ex = string.IsNullOrWhiteSpace(exchange) ? "binance" : exchange.Trim().ToLowerInvariant();
        var mt = string.IsNullOrWhiteSpace(marketType) ? "spot" : marketType.Trim().ToLowerInvariant();

        var decodedSymbol = Uri.UnescapeDataString(symbol).Trim();
        var normalizedRequest = new OnDemandRequest(
            decodedSymbol,
            timeframe.Trim().ToLowerInvariant(),
            ex,
            mt);
        var notification = await collectorGateway.AnalyzeOnDemandAsync(normalizedRequest, ct).ConfigureAwait(false);
        if (notification is null)
            return Results.StatusCode(502);

        var result = notification.Result;
        await analysisResultStore.StoreAsync(result, ct).ConfigureAwait(false);

        var response = new IndicatorResponseDto(ToSummary(result), detail ? result.Indicators : null);
        return Results.Ok(response);
    }

    private static IndicatorSummaryDto ToSummary(AnalysisResult result)
    {
        return new IndicatorSummaryDto(
            result.Symbol,
            result.Timeframe,
            result.MarketStructure.MidTermTrend.ToString(),
            result.Decision.ToString(),
            result.CurrentPrice,
            result.Reason,
            result.AnalyzedAtUtc);
    }
}
