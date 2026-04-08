using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.Telegram.Services;

namespace NetGding.Telegram.Endpoints;

public static class NotifyEndpoints
{
    public static void MapNotifyEndpoints(this WebApplication app)
    {
        app.MapPost("/internal/telegram/notify", HandleNotifyAsync)
           .WithName("TelegramNotify")
           .WithTags("Telegram");
    }

    private static async Task<IResult> HandleNotifyAsync(
        [FromBody] AnalysisResult result,
        ITelegramNotifier notifier,
        IAnalysisStore store,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        store.Store(result);

        try
        {
            await notifier.SendAnalysisAsync(result, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Telegram notification sent for {Symbol} ({Timeframe}) → Decision={Decision}",
                result.Symbol, result.Timeframe, result.Decision);

            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram notification for {Symbol}", result.Symbol);
            return Results.StatusCode(502);
        }
    }
}