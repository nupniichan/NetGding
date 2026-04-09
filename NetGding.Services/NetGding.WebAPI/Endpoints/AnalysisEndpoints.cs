using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using NetGding.Contracts.Models.Analysis;
using NetGding.Configurations.Options;
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
    }

    private static async Task<IResult> HandleOnDemandAsync(
        [FromBody] OnDemandRequest request,
        IHttpClientFactory httpFactory,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol) || string.IsNullOrWhiteSpace(request.Timeframe))
            return Results.BadRequest("Symbol and Timeframe are required.");

        var o = options.CurrentValue;
        var url = $"{o.CollectorServiceUrl.TrimEnd('/')}/api/analysis/on-demand";

        for (var attempt = 1; attempt <= o.MaxRetries; attempt++)
        {
            try
            {
                var http = httpFactory.CreateClient(nameof(TelegramForwarder));
                var response = await http.PostAsJsonAsync(url, request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<AnalysisResult>(cancellationToken: ct)
                    .ConfigureAwait(false);

                return result is null ? Results.StatusCode(502) : Results.Ok(result);
            }
            catch (Exception ex) when (attempt < o.MaxRetries)
            {
                logger.LogWarning(ex,
                    "On-demand proxy: attempt {Attempt}/{MaxRetries} failed for {Symbol} ({Timeframe})",
                    attempt, o.MaxRetries, request.Symbol, request.Timeframe);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "On-demand proxy failed for {Symbol} ({Timeframe})",
                    request.Symbol, request.Timeframe);
                return Results.StatusCode(502);
            }
        }

        return Results.StatusCode(502);
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