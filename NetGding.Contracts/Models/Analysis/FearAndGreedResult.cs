using System;

namespace NetGding.Contracts.Models.Analysis;

public sealed class FearAndGreedResult
{
    public int Value { get; set; }
    public string Classification { get; set; } = "Neutral";
    public DateTime TimestampUtc { get; set; }
}
