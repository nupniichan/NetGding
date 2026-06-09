using System;

namespace NetGding.WebApi.Models;

public sealed record IndicatorSummaryDto(
    string Symbol,
    string Timeframe,
    string Trend,
    string Decision,
    decimal CurrentPrice,
    string Reason,
    DateTime AnalyzedAtUtc);
