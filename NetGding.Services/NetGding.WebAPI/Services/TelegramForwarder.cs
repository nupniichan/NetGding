using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Configurations.Options;

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

    public async Task ForwardAsync(AnalysisResult result, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        var url = $"{o.TelegramServiceUrl.TrimEnd('/')}/internal/telegram/notify";

        var maxRetries = Math.Max(1, o.MaxRetries);
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient(nameof(TelegramForwarder));
                var response = await http.PostAsJsonAsync(url, result, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex,
                    "TelegramForwarder: attempt {Attempt}/{MaxRetries} failed for {Symbol}",
                    attempt, maxRetries, result.Symbol);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }
    }
}