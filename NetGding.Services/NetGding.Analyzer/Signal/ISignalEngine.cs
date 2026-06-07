using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Analyzer.Signal;

public interface ISignalEngine
{
    SignalResult Evaluate(LlmSignal signal, IndicatorSnapshot indicators, string symbol, MarketRegime regime);
}