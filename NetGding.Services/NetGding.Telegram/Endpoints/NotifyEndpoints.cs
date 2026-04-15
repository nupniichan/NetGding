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
        [FromBody] AnalysisNotification notification,
        ITelegramNotifier notifier,
        IAnalysisStore store,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        store.Store(notification.Result);

        try
        {
            await notifier.SendAnalysisAsync(notification, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Telegram notification sent for {Symbol} ({Timeframe}) → Decision={Decision}",
                notification.Result.Symbol, notification.Result.Timeframe, notification.Result.Decision);

            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram notification for {Symbol}", notification.Result.Symbol);
            return Results.StatusCode(502);
        }
    }
}
