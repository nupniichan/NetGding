using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public sealed class TelegramForwarder : ITelegramForwarder
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<TelegramForwarder> _logger;

    public TelegramForwarder(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<TelegramForwarder> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ForwardAsync(AnalysisNotification notification, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        var url = $"{o.TelegramServiceUrl.TrimEnd('/')}/internal/telegram/notify";

        var response = await HttpRetryHelper.ExecuteAsync(
            () =>
            {
                var http = _httpFactory.CreateClient(nameof(TelegramForwarder));
                return http.PostAsJsonAsync(url, notification, ct);
            },
            maxRetries: Math.Max(1, o.MaxRetries),
            baseDelaySeconds: 2,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "TelegramForwarder: attempt {Attempt}/{Max} failed (status={Status}) for {Symbol}",
                attempt, max, status, notification.Result.Symbol),
            ct: ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
