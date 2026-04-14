using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface IDiscordForwarder
{
    Task ForwardAsync(AnalysisResult result, CancellationToken ct = default);
}
