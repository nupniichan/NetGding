using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;

namespace NetGding.Analyzer.Signal;

public sealed class SignalEngine : ISignalEngine
{
    private readonly SignalEngineOptions _options;
    private readonly ILogger<SignalEngine> _logger;
    private readonly ConcurrentDictionary<string, TradeDecision> _lastSignal = new(StringComparer.OrdinalIgnoreCase);

    public SignalEngine(IOptions<SignalEngineOptions> options, ILogger<SignalEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public SignalResult Evaluate(LlmSignal signal, IndicatorSnapshot indicators, string symbol)
    {
        if (signal.Confidence < _options.MinConfidence)
        {
            _logger.LogDebug(
                "SignalEngine [{Symbol}]: rejected — confidence {Confidence:F2} < threshold {Threshold:F2}",
                symbol, signal.Confidence, _options.MinConfidence);

            return new SignalResult
            {
                Decision = TradeDecision.Wait,
                RejectionReason = $"Confidence {signal.Confidence:F2} below minimum {_options.MinConfidence:F2}"
            };
        }

        var candidate = DetermineCandidate(signal);

        if (candidate == TradeDecision.Wait)
        {
            return new SignalResult
            {
                Decision = TradeDecision.Wait,
                RejectionReason = "Signals not aligned for a trade"
            };
        }

        var guardResult = ApplyEmaGuardrail(candidate, indicators, symbol);
        if (guardResult is not null) return guardResult;

        var stabilityResult = ApplyStabilityFilter(candidate, signal, symbol);
        if (stabilityResult is not null) return stabilityResult;

        _lastSignal[symbol] = candidate;

        _logger.LogInformation(
            "SignalEngine [{Symbol}]: {Decision} — confidence {Confidence:F2}, trend={Trend}, momentum={Momentum}",
            symbol, candidate, signal.Confidence, signal.Trend, signal.Momentum);

        return new SignalResult { Decision = candidate };
    }

    private TradeDecision DetermineCandidate(LlmSignal signal)
    {
        if (signal.Trend == TrendBias.Bullish
            && signal.Momentum == MomentumState.Strong
            && signal.Confidence >= _options.TradeConfidence)
            return TradeDecision.Buy;

        if (signal.Trend == TrendBias.Bearish
            && signal.Momentum == MomentumState.Strong
            && signal.Confidence >= _options.TradeConfidence)
            return TradeDecision.Sell;

        return TradeDecision.Wait;
    }

    private SignalResult? ApplyEmaGuardrail(TradeDecision candidate, IndicatorSnapshot indicators, string symbol)
    {
        if (!indicators.Ema.TryGetValue(_options.FastEmaPeriod, out var fast) ||
            !indicators.Ema.TryGetValue(_options.SlowEmaPeriod, out var slow))
            return null;

        if (candidate == TradeDecision.Buy && fast < slow)
        {
            _logger.LogDebug(
                "SignalEngine [{Symbol}]: BUY rejected — EMA({Fast})={FastVal:F4} < EMA({Slow})={SlowVal:F4}",
                symbol, _options.FastEmaPeriod, fast, _options.SlowEmaPeriod, slow);

            return new SignalResult
            {
                Decision = TradeDecision.Wait,
                RejectionReason = $"EMA guardrail: fast EMA({_options.FastEmaPeriod}) below slow EMA({_options.SlowEmaPeriod})"
            };
        }

        if (candidate == TradeDecision.Sell && fast > slow)
        {
            _logger.LogDebug(
                "SignalEngine [{Symbol}]: SELL rejected — EMA({Fast})={FastVal:F4} > EMA({Slow})={SlowVal:F4}",
                symbol, _options.FastEmaPeriod, fast, _options.SlowEmaPeriod, slow);

            return new SignalResult
            {
                Decision = TradeDecision.Wait,
                RejectionReason = $"EMA guardrail: fast EMA({_options.FastEmaPeriod}) above slow EMA({_options.SlowEmaPeriod})"
            };
        }

        return null;
    }

    private SignalResult? ApplyStabilityFilter(TradeDecision candidate, LlmSignal signal, string symbol)
    {
        if (!_lastSignal.TryGetValue(symbol, out var last))
            return null;

        var isReversal = (last == TradeDecision.Buy && candidate == TradeDecision.Sell)
                      || (last == TradeDecision.Sell && candidate == TradeDecision.Buy);

        if (!isReversal) return null;

        if (signal.Confidence < _options.ReversalConfidence)
        {
            _logger.LogDebug(
                "SignalEngine [{Symbol}]: reversal {Last}→{New} suppressed — confidence {Confidence:F2} < {Threshold:F2}",
                symbol, last, candidate, signal.Confidence, _options.ReversalConfidence);

            return new SignalResult
            {
                Decision = TradeDecision.Wait,
                RejectionReason = $"Reversal suppressed: confidence {signal.Confidence:F2} below reversal threshold {_options.ReversalConfidence:F2}"
            };
        }

        return null;
    }
}