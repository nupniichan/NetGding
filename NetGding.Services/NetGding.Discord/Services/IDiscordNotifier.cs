using NetGding.Contracts.Models.Analysis;

namespace NetGding.Discord.Services;

public interface IDiscordNotifier
{
    Task SendAnalysisAsync(AnalysisNotification notification, CancellationToken ct = default);
    Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default);
}
