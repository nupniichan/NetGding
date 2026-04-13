using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Analyzer.Signal;

public sealed class SignalResult
{
    public TradeDecision Decision { get; init; }
    public string RejectionReason { get; init; } = "";
    public bool WasRejected => Decision == TradeDecision.Wait && !string.IsNullOrEmpty(RejectionReason);
}