using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface ITelegramForwarder
{
    Task ForwardAsync(AnalysisNotification notification, CancellationToken ct = default);
}
