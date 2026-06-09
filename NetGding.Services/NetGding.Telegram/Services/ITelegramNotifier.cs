using NetGding.Contracts.Models.Analysis;

namespace NetGding.Telegram.Services;

public interface ITelegramNotifier
{
    Task SendAnalysisAsync(AnalysisNotification notification, CancellationToken ct = default);
    Task SendTextAsync(string chatId, string text, CancellationToken ct = default);
    Task SendPhotoAsync(string chatId, byte[] photoBytes, string fileName, string caption, CancellationToken ct = default);
}
