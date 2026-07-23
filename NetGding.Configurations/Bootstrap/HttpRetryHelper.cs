namespace NetGding.Configurations.Bootstrap;

public static class HttpRetryHelper
{
    public static async Task<HttpResponseMessage> ExecuteAsync(
        Func<Task<HttpResponseMessage>> action,
        int maxRetries,
        int baseDelaySeconds,
        Action<int, int, int>? onRetry = null,
        CancellationToken ct = default)
    {
        return await action().ConfigureAwait(false);
    }
}