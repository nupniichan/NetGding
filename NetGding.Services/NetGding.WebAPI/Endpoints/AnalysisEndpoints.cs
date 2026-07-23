using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Exceptions;
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

        app.MapGet("/api/market/dom", HandleGetDomAsync)
           .WithName("GetDom")
           .WithTags("Market");

        app.MapGet("/api/analysis/latest/{*symbol}", HandleGetLatestAsync)
           .WithName("GetLatestAnalysis")
           .WithTags("Analysis");

        app.MapGet("/api/analysis/history/{*symbol}", HandleGetHistoryAsync)
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
        if (string.IsNullOrWhiteSpace(request.Symbol) ||
            string.IsNullOrWhiteSpace(request.Timeframe))
        {
            return Results.BadRequest("Symbol and Timeframe are required.");
        }

        var exchange = string.IsNullOrWhiteSpace(request.Exchange) ? "binance" : request.Exchange.Trim().ToLowerInvariant();
        var marketType = string.IsNullOrWhiteSpace(request.MarketType) ? "spot" : request.MarketType.Trim().ToLowerInvariant();

        var normalizedRequest = new OnDemandRequest(
            request.Symbol.Trim(),
            request.Timeframe.Trim().ToLowerInvariant(),
            exchange,
            marketType,
            request.ChartSymbol?.Trim(),
            request.ChartOnly);
        try
        {
            var notification = await collectorGateway.AnalyzeOnDemandAsync(normalizedRequest, ct).ConfigureAwait(false);
            if (notification is null)
            {
                logger.LogError("On-demand proxy failed for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                    normalizedRequest.Symbol,
                    normalizedRequest.Timeframe,
                    normalizedRequest.Exchange,
                    normalizedRequest.MarketType);
                var errorResponse = new ErrorResponse(
                    ErrorCodes.ProxyFailed,
                    "WebAPI.HandleOnDemandAsync",
                    "Collector service returned empty response.");
                return Results.Json(errorResponse, statusCode: 502);
            }

            return Results.Ok(notification);
        }
        catch (NetGdingException ex)
        {
            logger.LogError(ex, "On-demand analysis NetGdingException in WebAPI for {Symbol} [{ErrorCode} at {Location}]: {Message}",
                normalizedRequest.Symbol, ex.ErrorCode, ex.Location, ex.Message);
            var errorResponse = new ErrorResponse(ex.ErrorCode, ex.Location, ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "On-demand analysis unexpected exception in WebAPI for {Symbol}", normalizedRequest.Symbol);
            var errorResponse = new ErrorResponse(
                ErrorCodes.InternalError,
                "WebAPI.HandleOnDemandAsync",
                ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
    }

    private static IResult HandleGetLatestAsync(
        [FromRoute] string symbol,
        [FromQuery] string timeframe,
        IAnalysisResultStore analysisResultStore)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(timeframe))
            return Results.BadRequest("Symbol and timeframe are required.");

        var decodedSymbol = Uri.UnescapeDataString(symbol).Trim();
        var latest = analysisResultStore.GetLatest(decodedSymbol, timeframe);
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

        var decodedSymbol = Uri.UnescapeDataString(symbol).Trim();
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

        var items = analysisResultStore.GetHistory(
            decodedSymbol,
            timeframe,
            from,
            to,
            normalizedPage,
            normalizedPageSize);

        return Results.Ok(new
        {
            Symbol = decodedSymbol,
            Timeframe = timeframe.Trim(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            Count = items.Count,
            Items = items
        });
    }

    private static async Task<IResult> HandleGetDomAsync(
        [FromQuery] string symbol,
        [FromQuery] string? exchange,
        [FromQuery] string? marketType,
        [FromQuery] int limit,
        ICollectorGateway collectorGateway,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return Results.BadRequest("Symbol is required.");
        }

        var ex = string.IsNullOrWhiteSpace(exchange) ? "binance" : exchange.Trim().ToLowerInvariant();
        var mt = string.IsNullOrWhiteSpace(marketType) ? "spot" : marketType.Trim().ToLowerInvariant();

        if (limit <= 0) limit = 10;

        try
        {
            var depth = await collectorGateway.GetDepthAsync(
                symbol.Trim(),
                ex,
                mt,
                limit,
                ct).ConfigureAwait(false);

            if (depth is null)
            {
                var errorResponse = new ErrorResponse(
                    ErrorCodes.DomEmpty,
                    "WebAPI.HandleGetDomAsync",
                    "Order book depth not found.");
                return Results.Json(errorResponse, statusCode: 404);
            }

            return Results.Ok(depth);
        }
        catch (NetGdingException ex)
        {
            logger.LogError(ex, "Get DOM NetGdingException in WebAPI for {Symbol} [{ErrorCode} at {Location}]: {Message}",
                symbol, ex.ErrorCode, ex.Location, ex.Message);
            var errorResponse = new ErrorResponse(ex.ErrorCode, ex.Location, ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to proxy DOM for {Symbol} on {Exchange} ({MarketType})", symbol, exchange, marketType);
            var errorResponse = new ErrorResponse(
                ErrorCodes.InternalError,
                "WebAPI.HandleGetDomAsync",
                ex.Message);
            return Results.Json(errorResponse, statusCode: 502);
        }
    }
}
