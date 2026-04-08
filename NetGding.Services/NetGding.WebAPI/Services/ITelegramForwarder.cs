using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public interface ITelegramForwarder
{
    Task ForwardAsync(AnalysisResult result, CancellationToken ct = default);
}