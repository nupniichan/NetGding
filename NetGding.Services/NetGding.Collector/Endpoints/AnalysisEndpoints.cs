using Microsoft.AspNetCore.Mvc;
using NetGding.Collector.Services;
using NetGding.Collector.Services.MarketData;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.Collector.Endpoints;

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
                    request.ChartSymbol?.Trim(),
                    request.ChartOnly,
                    ct)
                .ConfigureAwait(false);

            return Results.Ok(notification);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (NetGding.Contracts.Exceptions.NetGdingException ex)
        {
            logger.LogError(ex, "On-demand analysis NetGdingException for {Symbol} [{ErrorCode} at {Location}]: {Message}",
                request.Symbol, ex.ErrorCode, ex.Location, ex.Message);
            var errorResponse = new NetGding.Contracts.Models.Analysis.ErrorResponse(ex.ErrorCode, ex.Location, ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "On-demand analysis HttpRequestException for {Symbol}: {Message}", request.Symbol, ex.Message);
            var errorResponse = new NetGding.Contracts.Models.Analysis.ErrorResponse(
                ErrorCodes.HttpRequestFailed,
                "Collector.OnDemandAsync",
                ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "On-demand analysis failed for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                request.Symbol, request.Timeframe, request.Exchange, request.MarketType);
            var errorResponse = new NetGding.Contracts.Models.Analysis.ErrorResponse(
                ErrorCodes.InternalError,
                "Collector.OnDemandAsync",
                ex.Message);
            return Results.Json(errorResponse, statusCode: 500);
        }
    }

    private static async Task<IResult> HandleGetDomAsync(
        [FromQuery] string symbol,
        [FromQuery] string exchange,
        [FromQuery] string marketType,
        [FromQuery] int limit,
        IMarketDataCollectorResolver collectorResolver,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(exchange) || string.IsNullOrWhiteSpace(marketType))
        {
            return Results.BadRequest("Symbol, exchange, and marketType are required.");
        }

        if (limit <= 0) limit = 10;

        try
        {
            if (!TryResolveMarketType(marketType, out var resolvedMarketType))
                return Results.BadRequest($"Invalid market type '{marketType}'.");

            var collector = collectorResolver.Resolve(exchange.Trim().ToLowerInvariant(), resolvedMarketType);
            var depth = await collector.GetDepthAsync(symbol.Trim(), limit, ct).ConfigureAwait(false);
            if (depth is null)
                return Results.NotFound("Failed to retrieve order book depth.");

            return Results.Ok(depth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get DOM for {Symbol} on {Exchange} ({MarketType})", symbol, exchange, marketType);
            return Results.StatusCode(500);
        }
    }

    private static bool TryResolveMarketType(string requested, out MarketType marketType) =>
        MarketParsingHelper.TryResolveMarketType(requested, out marketType);
}

