using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Services;

public sealed class DiscordForwarder : IDiscordForwarder
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<DiscordForwarder> _logger;

    public DiscordForwarder(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<WebApiOptions> options,
        ILogger<DiscordForwarder> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ForwardAsync(AnalysisNotification notification, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.DiscordServiceUrl))
            return;

        var url = $"{o.DiscordServiceUrl.TrimEnd('/')}/internal/discord/notify";

        var response = await HttpRetryHelper.ExecuteAsync(
            () =>
            {
                var http = _httpFactory.CreateClient(nameof(DiscordForwarder));
                return http.PostAsJsonAsync(url, notification, ct);
            },
            maxRetries: Math.Max(1, o.MaxRetries),
            baseDelaySeconds: 2,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "DiscordForwarder: attempt {Attempt}/{Max} failed (status={Status}) for {Symbol}",
                attempt, max, status, notification.Result.Symbol),
            ct: ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
