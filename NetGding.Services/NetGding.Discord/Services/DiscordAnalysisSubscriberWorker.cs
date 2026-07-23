using NetGding.Contracts.Events;
using NetGding.Contracts.Messaging;
using NetGding.Contracts.Services;

namespace NetGding.Discord.Services;

public sealed class DiscordAnalysisSubscriberWorker : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IAnalysisCache _cache;
    private readonly IDiscordNotifier _notifier;
    private readonly ILogger<DiscordAnalysisSubscriberWorker> _logger;

    public DiscordAnalysisSubscriberWorker(
        IEventBus eventBus,
        IAnalysisCache cache,
        IDiscordNotifier notifier,
        ILogger<DiscordAnalysisSubscriberWorker> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Discord] Starting DiscordAnalysisSubscriberWorker listening on {Topic}...", EventTopics.AnalysisCompleted);

        await _eventBus.SubscribeAsync<AnalysisCompletedEvent>(
            EventTopics.AnalysisCompleted,
            consumerGroup: "discord-group",
            consumerName: "discord-worker",
            handler: HandleAnalysisCompletedAsync,
            ct: stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleAnalysisCompletedAsync(AnalysisCompletedEvent evt, CancellationToken ct)
    {
        if (evt?.Notification?.Result is null)
            return;

        _logger.LogInformation("[Discord] Received AnalysisCompletedEvent for {Symbol} ({Timeframe})",
            evt.Notification.Result.Symbol, evt.Notification.Result.Timeframe);

        _cache.Store(evt.Notification.Result);
        await _notifier.SendAnalysisAsync(evt.Notification, ct).ConfigureAwait(false);
    }
}
