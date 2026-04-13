using DSharpPlus;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Discord.Commands;

namespace NetGding.Discord.Services;

public sealed class DiscordBotService : BackgroundService
{
    private readonly DiscordClient _client;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<DiscordOptions> _options;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordClient client,
        IServiceProvider serviceProvider,
        IOptionsMonitor<DiscordOptions> options,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(o.BotToken))
        {
            _logger.LogWarning("DiscordBotService: BotToken is not configured. Bot disabled.");
            return;
        }

        var slash = _client.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = _serviceProvider
        });

        if (o.GuildId != 0)
            slash.RegisterCommands<AnalysisCommands>(o.GuildId);
        else
            slash.RegisterCommands<AnalysisCommands>();

        _logger.LogInformation("DiscordBotService: connecting...");
        await _client.ConnectAsync().ConfigureAwait(false);
        _logger.LogInformation("DiscordBotService: connected as {Username}.", _client.CurrentUser?.Username);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        finally
        {
            _logger.LogInformation("DiscordBotService: disconnecting...");
            await _client.DisconnectAsync().ConfigureAwait(false);
        }
    }
}