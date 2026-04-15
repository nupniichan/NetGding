using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.ChartRenderer;

public interface IChartRenderer
{
    byte[] Render(IReadOnlyList<OhlcvBar> bars, AnalysisResult result);
}
