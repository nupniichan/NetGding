namespace NetGding.Contracts.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default);

    Task SubscribeAsync<T>(
        string streamName,
        string consumerGroup,
        string consumerName,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct = default);
}
