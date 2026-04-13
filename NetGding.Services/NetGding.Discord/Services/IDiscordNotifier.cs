using NetGding.Contracts.Models.Analysis;

namespace NetGding.Discord.Services;

public interface IDiscordNotifier
{
    Task SendAnalysisAsync(AnalysisResult result, CancellationToken ct = default);
    Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default);
}