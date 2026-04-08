using NetGding.Contracts.Models.Analysis;

namespace NetGding.Telegram.Services;

public interface ITelegramNotifier
{
    Task SendAnalysisAsync(AnalysisResult result, CancellationToken ct = default);
    Task SendTextAsync(string chatId, string text, CancellationToken ct = default);
}