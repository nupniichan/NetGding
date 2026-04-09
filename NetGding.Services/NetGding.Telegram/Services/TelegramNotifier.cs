using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Configurations.Options;
using NetGding.Telegram.Formatting;

namespace NetGding.Telegram.Services;

public sealed class TelegramNotifier : ITelegramNotifier
{
    private const string ParseMode = "MarkdownV2";
    private const int MaxMessageLength = 4096;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly AnalysisMessageFormatter _formatter;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<TelegramOptions> options,
        AnalysisMessageFormatter formatter,
        ILogger<TelegramNotifier> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _formatter = formatter;
        _logger = logger;
    }

    public Task SendAnalysisAsync(AnalysisResult result, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        var text = _formatter.Build(result);
        return SendTextAsync(o.ChatId, text, ct);
    }

    public async Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            _logger.LogWarning("TelegramNotifier: BotToken is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("TelegramNotifier: ChatId is not configured.");
            return;
        }

        var truncated = text.Length > MaxMessageLength
            ? text[..(MaxMessageLength - 3)] + "\\.\\.\\."
            : text;

        var url = $"{o.ApiBaseUrl.TrimEnd('/')}/bot{o.BotToken}/sendMessage";
        var payload = new { chat_id = chatId, text = truncated, parse_mode = ParseMode };

        var http = _httpFactory.CreateClient(nameof(TelegramNotifier));

        try
        {
            var response = await http.PostAsJsonAsync(url, payload, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError(
                    "TelegramNotifier: sendMessage failed ({StatusCode}): {Body}",
                    (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelegramNotifier: failed to send message to chat {ChatId}", chatId);
            throw;
        }
    }
}