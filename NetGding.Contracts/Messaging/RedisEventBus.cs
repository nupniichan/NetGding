using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Options;
using StackExchange.Redis;

namespace NetGding.Contracts.Messaging;

public sealed class RedisEventBus : IEventBus
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<RedisOptions> _options;
    private readonly ILogger<RedisEventBus> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public RedisEventBus(
        IConnectionMultiplexer redis,
        IOptionsMonitor<RedisOptions> options,
        ILogger<RedisEventBus> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic stream name cannot be null or empty.", nameof(topic));
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(message, s_jsonOptions);
        var maxLen = _options.CurrentValue.StreamMaxLen;

        var messageId = await db.StreamAddAsync(
            topic,
            "payload",
            json,
            maxLength: maxLen > 0 ? maxLen : 10000,
            useApproximateMaxLength: true).ConfigureAwait(false);

        _logger.LogDebug("[RedisEventBus] Published to {Topic} with MsgId={MsgId}, Type={Type}",
            topic, messageId, typeof(T).Name);
    }

    public async Task SubscribeAsync<T>(
        string streamName,
        string consumerGroup,
        string consumerName,
        Func<T, CancellationToken, Task> handler,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("Stream name cannot be null or empty.", nameof(streamName));
        if (string.IsNullOrWhiteSpace(consumerGroup))
            throw new ArgumentException("Consumer group cannot be null or empty.", nameof(consumerGroup));
        if (string.IsNullOrWhiteSpace(consumerName))
            throw new ArgumentException("Consumer name cannot be null or empty.", nameof(consumerName));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var db = _redis.GetDatabase();

        // Ensure consumer group exists
        try
        {
            await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "0-0", createStream: true).ConfigureAwait(false);
            _logger.LogInformation("[RedisEventBus] Created Consumer Group '{Group}' on stream '{Stream}'", consumerGroup, streamName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists, safe to ignore
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisEventBus] Error checking/creating consumer group '{Group}' on '{Stream}'", consumerGroup, streamName);
        }

        _logger.LogInformation("[RedisEventBus] Started listening on '{Stream}' for Group='{Group}', Consumer='{Consumer}'",
            streamName, consumerGroup, consumerName);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var entries = await db.StreamReadGroupAsync(
                        streamName,
                        consumerGroup,
                        consumerName,
                        ">",
                        count: 10).ConfigureAwait(false);

                    if (entries.Length == 0)
                    {
                        var pollInterval = Math.Max(50, _options.CurrentValue.ConsumerPollIntervalMs);
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        if (ct.IsCancellationRequested)
                            break;

                        var payloadValue = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;
                        if (payloadValue.IsNullOrEmpty)
                        {
                            await db.StreamAcknowledgeAsync(streamName, consumerGroup, entry.Id).ConfigureAwait(false);
                            continue;
                        }

                        try
                        {
                            var deserialized = JsonSerializer.Deserialize<T>(payloadValue.ToString(), s_jsonOptions);
                            if (deserialized is not null)
                            {
                                await handler(deserialized, ct).ConfigureAwait(false);
                            }
                            await db.StreamAcknowledgeAsync(streamName, consumerGroup, entry.Id).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[RedisEventBus] Handler error processing MsgId={MsgId} on {Stream} (Group={Group})",
                                entry.Id, streamName, consumerGroup);
                            // ACK failed messages to avoid infinite loop block or ack after dead-letter strategy if needed
                            await db.StreamAcknowledgeAsync(streamName, consumerGroup, entry.Id).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RedisEventBus] Exception in stream polling loop for '{Stream}'", streamName);
                    try
                    {
                        await Task.Delay(2000, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }

            _logger.LogInformation("[RedisEventBus] Stopped listening on '{Stream}' for Group='{Group}'", streamName, consumerGroup);
        }, ct);
    }
}
