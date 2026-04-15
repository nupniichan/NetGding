using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Discord.Formatting;

namespace NetGding.Discord.Services;

public sealed class DiscordNotifier : IDiscordNotifier
{
    private readonly DiscordClient _client;
    private readonly IOptionsMonitor<DiscordOptions> _options;
    private readonly AnalysisEmbedFormatter _formatter;
    private readonly ILogger<DiscordNotifier> _logger;

    public DiscordNotifier(
        DiscordClient client,
        IOptionsMonitor<DiscordOptions> options,
        AnalysisEmbedFormatter formatter,
        ILogger<DiscordNotifier> logger)
    {
        _client = client;
        _options = options;
        _formatter = formatter;
        _logger = logger;
    }

    public async Task SendAnalysisAsync(AnalysisNotification notification, CancellationToken ct = default)
    {
        var o = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            _logger.LogWarning("DiscordNotifier: BotToken is not configured.");
            return;
        }

        if (o.ChannelId == 0)
        {
            _logger.LogWarning("DiscordNotifier: ChannelId is not configured.");
            return;
        }

        try
        {
            var channel = await _client.GetChannelAsync(o.ChannelId).ConfigureAwait(false);
            var embed = _formatter.Build(notification.Result);

            if (!string.IsNullOrWhiteSpace(notification.ChartImageBase64))
            {
                var chartBytes = Convert.FromBase64String(notification.ChartImageBase64);
                using var ms = new MemoryStream(chartBytes);

                var messageBuilder = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddFile("chart.png", ms);

                await channel.SendMessageAsync(messageBuilder).ConfigureAwait(false);
            }
            else
            {
                await channel.SendMessageAsync(embed).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DiscordNotifier: failed to send analysis for {Symbol} to channel {ChannelId}",
                notification.Result.Symbol, o.ChannelId);
            throw;
        }
    }

    public async Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default)
    {
        try
        {
            var channel = await _client.GetChannelAsync(channelId).ConfigureAwait(false);
            await channel.SendMessageAsync(text).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DiscordNotifier: failed to send text to channel {ChannelId}", channelId);
            throw;
        }
    }
}
