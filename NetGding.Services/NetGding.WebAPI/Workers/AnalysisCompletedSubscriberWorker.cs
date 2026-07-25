using NetGding.Contracts.Messaging;
using NetGding.Contracts.Models.Analysis;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Workers;

public sealed class AnalysisCompletedSubscriberWorker : AnalysisSubscriberWorkerBase
{
    private readonly IServiceProvider _serviceProvider;

    public AnalysisCompletedSubscriberWorker(
        IEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<AnalysisCompletedSubscriberWorker> logger)
        : base(eventBus, "webapi-group", "webapi-worker", logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    protected override async Task OnAnalysisCompletedAsync(AnalysisNotification notification, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAnalysisResultStore>();
        await store.StoreAsync(notification.Result, ct).ConfigureAwait(false);
    }
}
