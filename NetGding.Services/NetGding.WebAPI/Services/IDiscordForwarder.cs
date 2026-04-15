using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface IDiscordForwarder
{
    Task ForwardAsync(AnalysisNotification notification, CancellationToken ct = default);
}
