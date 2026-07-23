namespace NetGding.Contracts.Events;

public sealed record FearAndGreedEvent(
    int Value,
    string Classification,
    DateTime TimestampUtc);
