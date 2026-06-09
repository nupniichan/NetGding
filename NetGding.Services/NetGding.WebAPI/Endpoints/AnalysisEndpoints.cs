using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;
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

        app.MapPost("/api/analysis/publish", HandlePublishAsync)
           .WithName("PublishAnalysis")
           .WithTags("Analysis");

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
            string.IsNullOrWhiteSpace(request.Timeframe) ||
            string.IsNullOrWhiteSpace(request.Exchange) ||
            string.IsNullOrWhiteSpace(request.MarketType))
        {
            return Results.BadRequest("Symbol, Timeframe, Exchange, and MarketType are required.");
        }

        var normalizedRequest = new OnDemandRequest(
            request.Symbol.Trim(),
            request.Timeframe.Trim().ToLowerInvariant(),
            request.Exchange.Trim().ToLowerInvariant(),
            request.MarketType.Trim().ToLowerInvariant(),
            request.ChartSymbol?.Trim(),
            request.ChartOnly);
        var notification = await collectorGateway.AnalyzeOnDemandAsync(normalizedRequest, ct).ConfigureAwait(false);
        if (notification is null)
        {
            logger.LogError("On-demand proxy failed for {Symbol} ({Timeframe}, {Exchange}, {MarketType})",
                normalizedRequest.Symbol,
                normalizedRequest.Timeframe,
                normalizedRequest.Exchange,
                normalizedRequest.MarketType);
            return Results.StatusCode(502);
        }

        if (!normalizedRequest.ChartOnly && string.IsNullOrWhiteSpace(normalizedRequest.ChartSymbol))
        {
            analysisResultStore.Store(notification.Result);
        }

        return Results.Ok(notification);
    }

    private static async Task<IResult> HandlePublishAsync(
        [FromBody] AnalysisNotification notification,
        ITelegramForwarder telegramForwarder,
        IDiscordForwarder discordForwarder,
        IAnalysisResultStore analysisResultStore,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var result = notification.Result;

        if (string.IsNullOrWhiteSpace(result.Symbol) ||
            string.IsNullOrWhiteSpace(result.Timeframe) ||
            result.AnalyzedAtUtc == default)
        {
            return Results.BadRequest("Symbol, Timeframe, and AnalyzedAtUtc are required.");
        }

        try
        {
            analysisResultStore.Store(result);

            await Task.WhenAll(
                telegramForwarder.ForwardAsync(notification, ct),
                discordForwarder.ForwardAsync(notification, ct)
            ).ConfigureAwait(false);

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
        [FromQuery] string exchange,
        [FromQuery] string marketType,
        [FromQuery] int limit,
        ICollectorGateway collectorGateway,
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
            var depth = await collectorGateway.GetDepthAsync(
                symbol.Trim(),
                exchange.Trim().ToLowerInvariant(),
                marketType.Trim().ToLowerInvariant(),
                limit,
                ct).ConfigureAwait(false);

            if (depth is null)
                return Results.NotFound("Failed to retrieve order book depth.");

            return Results.Ok(depth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to proxy DOM for {Symbol} on {Exchange} ({MarketType})", symbol, exchange, marketType);
            return Results.StatusCode(502);
        }
    }
}
