using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetGding.Contracts.Events;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.Contracts.Messaging;

public abstract class AnalysisSubscriberWorkerBase : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly string _consumerGroup;
    private readonly string _consumerName;
    protected readonly ILogger Logger;

    protected AnalysisSubscriberWorkerBase(
        IEventBus eventBus,
        string consumerGroup,
        string consumerName,
        ILogger logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _consumerGroup = consumerGroup ?? throw new ArgumentNullException(nameof(consumerGroup));
        _consumerName = consumerName ?? throw new ArgumentNullException(nameof(consumerName));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation(
            "[{Service}] Starting subscriber worker listening on {Topic}...",
            _consumerName, EventTopics.AnalysisCompleted);

        await _eventBus.SubscribeAsync<AnalysisCompletedEvent>(
            EventTopics.AnalysisCompleted,
            consumerGroup: _consumerGroup,
            consumerName: _consumerName,
            handler: HandleAnalysisCompletedInternalAsync,
            ct: stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleAnalysisCompletedInternalAsync(AnalysisCompletedEvent evt, CancellationToken ct)
    {
        if (evt?.Notification?.Result is null)
            return;

        Logger.LogInformation(
            "[{Service}] Received AnalysisCompletedEvent for {Symbol} ({Timeframe})",
            _consumerName, evt.Notification.Result.Symbol, evt.Notification.Result.Timeframe);

        await OnAnalysisCompletedAsync(evt.Notification, ct).ConfigureAwait(false);
    }

    protected abstract Task OnAnalysisCompletedAsync(AnalysisNotification notification, CancellationToken ct);
}
