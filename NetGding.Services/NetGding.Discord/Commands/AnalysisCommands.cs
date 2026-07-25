using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Services;
using NetGding.Discord.Formatting;

namespace NetGding.Discord.Commands;

public sealed class AnalysisCommands : ApplicationCommandModule
{
    private readonly IAnalysisCache _store;
    private readonly AnalysisEmbedFormatter _formatter;
    private readonly IWebApiClient _webApiClient;
    private readonly IOptionsMonitor<DiscordOptions> _options;
    private readonly ILogger<AnalysisCommands> _logger;

    public AnalysisCommands(
        IAnalysisCache store,
        AnalysisEmbedFormatter formatter,
        IWebApiClient webApiClient,
        IOptionsMonitor<DiscordOptions> options,
        ILogger<AnalysisCommands> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _webApiClient = webApiClient ?? throw new ArgumentNullException(nameof(webApiClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                "• `/analyze <symbol> <timeframe> [exchange]` — run live analysis (default exchange: binance)\n" +
                "• `/chart [symbol] [timeframe] [exchange]` — get live chart (defaults: BTC, 4h, binance)\n" +
                "• `/news [symbol] [limit]` — get recent news (defaults: BTC, 5)\n" +
                "• `/dom [timeframe]` — check BTC dominance chart and DOM (default: 4h)\n" +
                "• `/fagi` — get the current Crypto Fear and Greed Index\n\n" +
                "**Supported timeframes:** `15m`, `1h`, `4h`, `1d`, `1w`, `1m`\n\n" +
                "**Supported exchanges:** `binance`, `okx`\n\n" +
                "**Indicator legend (shown on chart and legend):**\n" +
                "• EMAx — Exponential Moving Average\n" +
                "• BB — Bollinger Bands\n" +
                "• VWAP — Volume Weighted Average Price\n" +
                "• S/R — Support/Resistance levels\n" +
                "• Entry/SL/TP/Buy — Risk management price levels\n\n" +
                "**Examples:**\n" +
                "  `/analyze BTC 4h`\n" +
                "  `/analyze BTC 4h okx`\n" +
                "  `/chart BTC 4h`\n" +
                "  `/dom 4h`\n" +
                "  `/news BTC 5`\n" +
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
        var normalizedSymbol = ValidationConstants.NormalizeSymbol(symbol);
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
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m")] string timeframe,
        [Option("exchange", "Exchange: binance, okx")] string exchange = "binance")
    {
        var normalizedSymbol = ValidationConstants.NormalizeSymbol(symbol);
        timeframe = timeframe.Trim().ToLowerInvariant();
        exchange = exchange.Trim().ToLowerInvariant();
        const string marketType = "spot";

        if (!ValidationConstants.AllowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!ValidationConstants.AllowedExchanges.Contains(exchange))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported exchanges: `binance`, `okx`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var req = new OnDemandRequest(normalizedSymbol, timeframe, exchange, marketType);
            var o = _options.CurrentValue;
            var notification = await _webApiClient.FetchOnDemandAnalysisAsync(req, o.OnDemandMaxRetries, o.OnDemandRetryBaseDelaySeconds)
                .ConfigureAwait(false);

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
            _logger.LogError(ex, "AnalysisCommands: on-demand analysis failed for {Symbol} ({Timeframe})", normalizedSymbol, timeframe);
            await SendFormattedErrorAsync(ctx, "Analysis Failed", ex).ConfigureAwait(false);
        }
    }

    [SlashCommand("fagi", "Get the current Crypto Fear and Greed Index")]
    public async Task FagiAsync(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        try
        {
            var fng = await _webApiClient.FetchFearAndGreedAsync().ConfigureAwait(false);

            var emoji = AnalysisEmbedFormatter.GetFearAndGreedEmoji(fng.Value);
            var color = GetFearAndGreedColor(fng.Value);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Crypto Fear & Greed Index")
                .WithColor(color)
                .AddField("Value", fng.Value.ToString(), inline: true)
                .AddField("Classification", $"{emoji} {fng.Classification}", inline: true)
                .WithTimestamp(fng.TimestampUtc)
                .WithFooter("Data provided by CoinMarketCap")
                .Build();

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalysisCommands: failed to fetch Fear & Greed Index");
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to fetch Fear & Greed Index. Please try again in a moment.")).ConfigureAwait(false);
        }
    }

    [SlashCommand("chart", "Get live chart for a symbol")]
    public async Task ChartAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC (default: BTC)")] string symbol = "BTC",
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m (default: 4h)")] string timeframe = "4h",
        [Option("exchange", "Exchange: binance, okx (default: binance)")] string exchange = "binance")
    {
        var normalizedSymbol = ValidationConstants.NormalizeSymbol(symbol);
        timeframe = timeframe.Trim().ToLowerInvariant();
        exchange = exchange.Trim().ToLowerInvariant();
        const string marketType = "spot";

        if (!ValidationConstants.AllowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!ValidationConstants.AllowedExchanges.Contains(exchange))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported exchanges: `binance`, `okx`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var req = new OnDemandRequest(normalizedSymbol, timeframe, exchange, marketType, ChartOnly: true);
            var o = _options.CurrentValue;
            var notification = await _webApiClient.FetchOnDemandAnalysisAsync(req, o.OnDemandMaxRetries, o.OnDemandRetryBaseDelaySeconds)
                .ConfigureAwait(false);

            var embed = _formatter.BuildChartEmbed(notification.Result);

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
            _logger.LogError(ex, "AnalysisCommands: on-demand chart failed for {Symbol} ({Timeframe})", normalizedSymbol, timeframe);
            await SendFormattedErrorAsync(ctx, "Chart Generation Failed", ex).ConfigureAwait(false);
        }
    }

    [SlashCommand("news", "Get news articles for a symbol")]
    public async Task NewsAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC (default: BTC)")] string symbol = "BTC",
        [Option("limit", "Number of articles (default: 5)")] long limit = 5)
    {
        var normalizedSymbol = ValidationConstants.NormalizeSymbol(symbol);
        var normLimit = (int)Math.Clamp(limit, 1, 10);

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var articles = await _webApiClient.FetchNewsAsync(normalizedSymbol, normLimit).ConfigureAwait(false);
            var embed = _formatter.BuildNewsEmbed(normalizedSymbol, articles);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(embed)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalysisCommands: news fetch failed for {Symbol}", normalizedSymbol);
            await SendFormattedErrorAsync(ctx, "News Fetch Failed", ex).ConfigureAwait(false);
        }
    }

    [SlashCommand("dom", "Check DOM and chart for Bitcoin")]
    public async Task DomAsync(
        InteractionContext ctx,
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m (default: 4h)")] string timeframe = "4h")
    {
        var normalizedSymbol = "BTC/USD";
        var exchange = "binance";
        var marketType = "spot";
        timeframe = timeframe.Trim().ToLowerInvariant();

        if (!ValidationConstants.AllowedTimeframes.Contains(timeframe))
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
            var req = new OnDemandRequest(normalizedSymbol, timeframe, exchange, marketType, ChartSymbol: "CRYPTOCAP:BTC.D", ChartOnly: true);
            var o = _options.CurrentValue;
            var notification = await _webApiClient.FetchOnDemandAnalysisAsync(req, o.OnDemandMaxRetries, o.OnDemandRetryBaseDelaySeconds).ConfigureAwait(false);

            var embed = _formatter.BuildDomChartEmbed(notification.Result);

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
            _logger.LogError(ex, "AnalysisCommands: DOM or chart request failed for {Symbol}", normalizedSymbol);
            await SendFormattedErrorAsync(ctx, "Request Failed", ex).ConfigureAwait(false);
        }
    }

    private static async Task SendFormattedErrorAsync(InteractionContext ctx, string title, Exception ex)
    {
        var errorMsg = "Service is unavailable. Please try again in a moment.";
        if (ex is NetGdingException nex)
        {
            errorMsg = $"❌ **{title}**\n• **Code:** `{nex.ErrorCode}`\n• **Location:** `{nex.Location}`\n• **Message:** {nex.Message}";
        }
        else if (ex.InnerException is NetGdingException inex)
        {
            errorMsg = $"❌ **{title}**\n• **Code:** `{inex.ErrorCode}`\n• **Location:** `{inex.Location}`\n• **Message:** {inex.Message}";
        }

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(errorMsg)).ConfigureAwait(false);
    }

    private static DiscordColor GetFearAndGreedColor(int value) => value switch
    {
        <= 25 => new DiscordColor(0xD63031),
        <= 45 => new DiscordColor(0xE67E22),
        <= 55 => new DiscordColor(0xF1C40F),
        <= 75 => new DiscordColor(0x2ECC71),
        _ => new DiscordColor(0x00B894)
    };
}