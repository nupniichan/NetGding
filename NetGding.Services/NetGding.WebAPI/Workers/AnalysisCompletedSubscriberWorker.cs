using NetGding.Contracts.Events;
using NetGding.Contracts.Messaging;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Workers;

public sealed class AnalysisCompletedSubscriberWorker : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalysisCompletedSubscriberWorker> _logger;

    public AnalysisCompletedSubscriberWorker(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<AnalysisCompletedSubscriberWorker> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[WebAPI] Starting AnalysisCompletedSubscriberWorker listening on {Topic}...", EventTopics.AnalysisCompleted);

        await _eventBus.SubscribeAsync<AnalysisCompletedEvent>(
            EventTopics.AnalysisCompleted,
            consumerGroup: "webapi-group",
            consumerName: "webapi-worker",
            handler: HandleAnalysisCompletedAsync,
            ct: stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleAnalysisCompletedAsync(AnalysisCompletedEvent evt, CancellationToken ct)
    {
        if (evt?.Notification?.Result is null)
            return;

        _logger.LogInformation("[WebAPI] Received AnalysisCompletedEvent for {Symbol} ({Timeframe}) from {Source}",
            evt.Notification.Result.Symbol, evt.Notification.Result.Timeframe, evt.SourceService);

        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisResultStore>();
        await store.StoreAsync(evt.Notification.Result, ct).ConfigureAwait(false);
    }
}
