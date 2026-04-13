using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Discord.Formatting;
using NetGding.Discord.Services;

namespace NetGding.Discord.Commands;

public sealed class AnalysisCommands : ApplicationCommandModule
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly HashSet<string> s_allowedTimeframes =
        new(StringComparer.OrdinalIgnoreCase) { "15m", "1h", "4h", "1d", "1w", "1m" };

    private readonly IAnalysisStore _store;
    private readonly AnalysisEmbedFormatter _formatter;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<DiscordOptions> _options;
    private readonly ILogger<AnalysisCommands> _logger;

    public AnalysisCommands(
        IAnalysisStore store,
        AnalysisEmbedFormatter formatter,
        IHttpClientFactory httpFactory,
        IOptionsMonitor<DiscordOptions> options,
        ILogger<AnalysisCommands> logger)
    {
        _store = store;
        _formatter = formatter;
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    [SlashCommand("help", "Show available commands")]
    public async Task HelpAsync(InteractionContext ctx)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle("NetGding")
            .WithColor(new DiscordColor(0x5865F2))
            .WithDescription(
                "**Available commands:**\n\n" +
                "• `/help` — show available commands\n" +
                "• `/latest <symbol>` — get cached analysis for a symbol (D1+)\n" +
                "• `/analyze <symbol> <timeframe>` — run live analysis\n\n" +
                "**Supported timeframes:** `15m`, `1h`, `4h`, `1d`, `1w`, `1m`\n\n" +
                "**Examples:**\n" +
                "  `/analyze BTC/USD 4h`\n" +
                "  `/latest BTC/USD`\n\n" +
                "D1+ analysis results are pushed automatically after each bar.")
            .Build();

        await ctx.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed)).ConfigureAwait(false);
    }

    [SlashCommand("latest", "Get cached analysis for a symbol")]
    public async Task LatestAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC/USD")] string symbol)
    {
        var result = _store.GetLatest(symbol);

        if (result is null)
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"No analysis found for symbol: **{symbol}**")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        var embed = _formatter.Build(result);

        await ctx.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed)).ConfigureAwait(false);
    }

    [SlashCommand("analyze", "Run live analysis for a symbol")]
    public async Task AnalyzeAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC/USD")] string symbol,
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m")] string timeframe)
    {
        timeframe = timeframe.Trim().ToLowerInvariant();

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var symbolCandidates = symbol.Contains('/', StringComparison.Ordinal)
                ? [symbol]
                : new[] { symbol, $"{symbol}/USD" };

            AnalysisResult? result = null;
            Exception? lastError = null;

            foreach (var candidate in symbolCandidates)
            {
                try
                {
                    result = await FetchOnDemandAnalysisAsync(candidate, timeframe).ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (result is null)
                throw lastError ?? new InvalidOperationException("On-demand analysis failed.");

            _store.Store(result);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(_formatter.Build(result))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AnalysisCommands: on-demand analysis failed for {Symbol} ({Timeframe})",
                symbol, timeframe);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("Collector service is unavailable. Please try again in a moment."))
                .ConfigureAwait(false);
        }
    }

    private async Task<AnalysisResult> FetchOnDemandAnalysisAsync(string symbol, string timeframe)
    {
        var o = _options.CurrentValue;
        var maxAttempts = o.OnDemandMaxRetries;
        var url = $"{o.CollectorBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new { symbol, timeframe };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient("CollectorClient");
                var response = await http.PostAsJsonAsync(url, payload).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<AnalysisResult>(s_jsonOptions)
                        .ConfigureAwait(false);

                    return result ?? throw new InvalidOperationException("Collector returned empty response.");
                }

                _logger.LogWarning(
                    "AnalysisCommands: on-demand attempt {Attempt}/{Max} returned {StatusCode} for {Symbol} ({Timeframe})",
                    attempt, maxAttempts, (int)response.StatusCode, symbol, timeframe);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "AnalysisCommands: on-demand attempt {Attempt}/{Max} failed for {Symbol} ({Timeframe}), retrying",
                    attempt, maxAttempts, symbol, timeframe);
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * o.OnDemandRetryBaseDelaySeconds))
                .ConfigureAwait(false);
        }

        throw new HttpRequestException($"Collector unreachable after {maxAttempts} attempts.");
    }
}