using NetGding.Contracts.Models.Analysis;

namespace NetGding.Analyzer.Signal;

public interface ISignalEngine
{
    SignalResult Evaluate(LlmSignal signal, IndicatorSnapshot indicators, string symbol);
}