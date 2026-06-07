using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.ChartRenderer;

public interface IChartRenderer
{
    Task<byte[]> RenderAsync(
        IReadOnlyList<OhlcvBar> bars,
        AnalysisResult result,
        string exchange,
        CancellationToken cancellationToken = default);
}
