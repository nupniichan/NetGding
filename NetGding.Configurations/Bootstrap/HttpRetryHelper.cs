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
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await action().ConfigureAwait(false);

                if (response.IsSuccessStatusCode || attempt == maxRetries)
                    return response;

                onRetry?.Invoke(attempt, maxRetries, (int)response.StatusCode);
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                onRetry?.Invoke(attempt, maxRetries, 0);
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * baseDelaySeconds), ct).ConfigureAwait(false);
        }

        throw new HttpRequestException($"Request failed after {maxRetries} attempts.");
    }
}