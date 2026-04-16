using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Discord.Formatting;

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
                "**Indicator legend (shown outside chart):**\n" +
                "• EMAx — Exponential Moving Average\n" +
                "• BB — Bollinger Bands\n" +
                "• VWAP — Volume Weighted Average Price\n" +
                "• S/R — Support/Resistance levels\n" +
                "• Entry/SL/TP/Buy — Risk management price levels\n\n" +
                "**Examples:**\n" +
                "  `/analyze BTC 4h`\n" +
                "  `/latest BTC`\n\n" +
                "D1+ analysis results are pushed automatically after each bar.")
            .Build();

        await ctx.CreateResponseAsync(
            InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AddEmbed(embed)).ConfigureAwait(false);
    }

    [SlashCommand("latest", "Get cached analysis for a symbol")]
    public async Task LatestAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC")] string symbol)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var result = _store.GetLatest(normalizedSymbol);

        if (result is null)
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"No analysis found for symbol: **{normalizedSymbol}**")
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
        [Option("symbol", "Symbol e.g. BTC")] string symbol,
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m")] string timeframe)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
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
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe).ConfigureAwait(false);

            _store.Store(notification.Result);

            var embed = _formatter.Build(notification.Result);

            if (!string.IsNullOrWhiteSpace(notification.ChartImageBase64))
            {
                var chartBytes = Convert.FromBase64String(notification.ChartImageBase64);
                using var ms = new MemoryStream(chartBytes);

                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder()
                        .AddEmbed(embed)
                        .AddFile("chart.png", ms)).ConfigureAwait(false);
            }
            else
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(embed)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AnalysisCommands: on-demand analysis failed for {Symbol} ({Timeframe})",
                normalizedSymbol, timeframe);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent("Collector service is unavailable. Please try again in a moment."))
                .ConfigureAwait(false);
        }
    }

    private async Task<AnalysisNotification> FetchOnDemandAnalysisAsync(string symbol, string timeframe)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new { symbol, timeframe };

        var response = await HttpRetryHelper.ExecuteAsync(
            () => _httpFactory.CreateClient("WebApiClient").PostAsJsonAsync(url, payload),
            maxRetries: Math.Max(1, o.OnDemandMaxRetries),
            baseDelaySeconds: o.OnDemandRetryBaseDelaySeconds,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "AnalysisCommands: on-demand attempt {Attempt}/{Max} failed (status={Status}) for {Symbol} ({Timeframe})",
                attempt, max, status, symbol, timeframe)).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var notification = await response.Content
            .ReadFromJsonAsync<AnalysisNotification>(s_jsonOptions)
            .ConfigureAwait(false);

        return notification ?? throw new InvalidOperationException("WebAPI returned empty response.");
    }

    private static string NormalizeSymbol(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return normalized.Contains('/', StringComparison.Ordinal) ? normalized : $"{normalized}/USD";
    }
}