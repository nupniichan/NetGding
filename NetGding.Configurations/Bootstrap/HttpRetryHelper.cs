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
        maxRetries = Math.Max(1, maxRetries);
        baseDelaySeconds = Math.Max(1, baseDelaySeconds);

        HttpResponseMessage? lastResponse = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                lastResponse = await action().ConfigureAwait(false);

                if (lastResponse.IsSuccessStatusCode || attempt == maxRetries)
                {
                    return lastResponse;
                }

                var statusCode = (int)lastResponse.StatusCode;
                onRetry?.Invoke(attempt, maxRetries, statusCode);
            }
            catch (Exception) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                onRetry?.Invoke(attempt, maxRetries, 0);
            }

            var delayMs = (int)(Math.Pow(2, attempt - 1) * baseDelaySeconds * 1000);
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }

        return lastResponse!;
    }
}