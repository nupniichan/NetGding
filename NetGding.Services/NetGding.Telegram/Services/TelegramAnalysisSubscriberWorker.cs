using NetGding.Contracts.Messaging;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Services;

namespace NetGding.Telegram.Services;

public sealed class TelegramAnalysisSubscriberWorker : AnalysisSubscriberWorkerBase
{
    private readonly IAnalysisCache _cache;
    private readonly ITelegramNotifier _notifier;

    public TelegramAnalysisSubscriberWorker(
        IEventBus eventBus,
        IAnalysisCache cache,
        ITelegramNotifier notifier,
        ILogger<TelegramAnalysisSubscriberWorker> logger)
        : base(eventBus, "telegram-group", "telegram-worker", logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    protected override async Task OnAnalysisCompletedAsync(AnalysisNotification notification, CancellationToken ct)
    {
        _cache.Store(notification.Result);
        await _notifier.SendAnalysisAsync(notification, ct).ConfigureAwait(false);
    }
}
