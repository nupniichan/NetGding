using NetGding.Contracts.Messaging;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Services;

namespace NetGding.Discord.Services;

public sealed class DiscordAnalysisSubscriberWorker : AnalysisSubscriberWorkerBase
{
    private readonly IAnalysisCache _cache;
    private readonly IDiscordNotifier _notifier;

    public DiscordAnalysisSubscriberWorker(
        IEventBus eventBus,
        IAnalysisCache cache,
        IDiscordNotifier notifier,
        ILogger<DiscordAnalysisSubscriberWorker> logger)
        : base(eventBus, "discord-group", "discord-worker", logger)
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
