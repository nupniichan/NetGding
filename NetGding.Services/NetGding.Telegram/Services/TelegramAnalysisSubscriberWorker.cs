using NetGding.Contracts.Events;
using NetGding.Contracts.Messaging;
using NetGding.Contracts.Services;

namespace NetGding.Telegram.Services;

public sealed class TelegramAnalysisSubscriberWorker : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IAnalysisCache _cache;
    private readonly ITelegramNotifier _notifier;
    private readonly ILogger<TelegramAnalysisSubscriberWorker> _logger;

    public TelegramAnalysisSubscriberWorker(
        IEventBus eventBus,
        IAnalysisCache cache,
        ITelegramNotifier notifier,
        ILogger<TelegramAnalysisSubscriberWorker> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Telegram] Starting TelegramAnalysisSubscriberWorker listening on {Topic}...", EventTopics.AnalysisCompleted);

        await _eventBus.SubscribeAsync<AnalysisCompletedEvent>(
            EventTopics.AnalysisCompleted,
            consumerGroup: "telegram-group",
            consumerName: "telegram-worker",
            handler: HandleAnalysisCompletedAsync,
            ct: stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleAnalysisCompletedAsync(AnalysisCompletedEvent evt, CancellationToken ct)
    {
        if (evt?.Notification?.Result is null)
            return;

        _logger.LogInformation("[Telegram] Received AnalysisCompletedEvent for {Symbol} ({Timeframe})",
            evt.Notification.Result.Symbol, evt.Notification.Result.Timeframe);

        _cache.Store(evt.Notification.Result);
        await _notifier.SendAnalysisAsync(evt.Notification, ct).ConfigureAwait(false);
    }
}
