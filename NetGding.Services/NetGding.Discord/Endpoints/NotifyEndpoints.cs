using Microsoft.AspNetCore.Mvc;
using NetGding.Contracts.Models.Analysis;
using NetGding.Discord.Services;

namespace NetGding.Discord.Endpoints;

public static class NotifyEndpoints
{
    public static void MapNotifyEndpoints(this WebApplication app)
    {
        app.MapPost("/internal/discord/notify", HandleNotifyAsync)
           .WithName("DiscordNotify")
           .WithTags("Discord");
    }

    private static async Task<IResult> HandleNotifyAsync(
        [FromBody] AnalysisResult result,
        IDiscordNotifier notifier,
        IAnalysisStore store,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        store.Store(result);

        try
        {
            await notifier.SendAnalysisAsync(result, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Discord notification sent for {Symbol} ({Timeframe}) -> Decision={Decision}",
                result.Symbol, result.Timeframe, result.Decision);

            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord notification for {Symbol}", result.Symbol);
            return Results.StatusCode(502);
        }
    }
}