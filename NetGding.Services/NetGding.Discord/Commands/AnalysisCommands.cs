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
using NetGding.Contracts.Models.MarketData;
using NetGding.Discord.Formatting;

using NetGding.Contracts.Services;

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
    private static readonly HashSet<string> s_allowedExchanges =
        new(StringComparer.OrdinalIgnoreCase) { "binance", "okx" };
    private static readonly HashSet<string> s_allowedMarketTypes =
        new(StringComparer.OrdinalIgnoreCase) { "spot", "future" };

    private readonly IAnalysisCache _store;
    private readonly AnalysisEmbedFormatter _formatter;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<DiscordOptions> _options;
    private readonly ILogger<AnalysisCommands> _logger;

    public AnalysisCommands(
        IAnalysisCache store,
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
                "• `/analyze <symbol> <timeframe> [exchange] [market_type]` — run live analysis (defaults: binance, spot)\n" +
                "• `/chart [symbol] [timeframe] [exchange] [market_type]` — get live chart (defaults: BTC, 4h, binance, spot)\n" +
                "• `/news [symbol] [limit]` — get recent news (defaults: BTC, 5)\n" +
                "• `/dom [timeframe]` — check BTC dominance chart and DOM (default: 4h)\n" +
                "• `/fagi` — get the current Crypto Fear and Greed Index\n\n" +
                "**Supported timeframes:** `15m`, `1h`, `4h`, `1d`, `1w`, `1m`\n\n" +
                "**Supported exchanges:** `binance`, `okx`\n" +
                "**Supported market types:** `spot`, `future`\n\n" +
                "**Indicator legend (shown on chart and legend):**\n" +
                "• EMAx — Exponential Moving Average\n" +
                "• BB — Bollinger Bands\n" +
                "• VWAP — Volume Weighted Average Price\n" +
                "• S/R — Support/Resistance levels\n" +
                "• Entry/SL/TP/Buy — Risk management price levels\n\n" +
                "**Examples:**\n" +
                "  `/analyze BTC 4h`\n" +
                "  `/analyze BTC 4h okx future`\n" +
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
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m")] string timeframe,
        [Option("exchange", "Exchange: binance, okx")] string exchange = "binance",
        [Option("market_type", "Market type: spot, future")] string marketType = "spot")
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        timeframe = timeframe.Trim().ToLowerInvariant();
        exchange = exchange.Trim().ToLowerInvariant();
        marketType = marketType.Trim().ToLowerInvariant();

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported exchanges: `binance`, `okx`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedMarketTypes.Contains(marketType))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported market types: `spot`, `future`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe, exchange, marketType)
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
            _logger.LogError(ex,
                "AnalysisCommands: on-demand analysis failed for {Symbol} ({Timeframe})",
                normalizedSymbol, timeframe);

            var errorMsg = "Collector service is unavailable. Please try again in a moment.";
            if (ex is NetGding.Contracts.Exceptions.NetGdingException nex)
            {
                errorMsg = $"❌ **Analysis Failed**\n• **Code:** `{nex.ErrorCode}`\n• **Location:** `{nex.Location}`\n• **Message:** {nex.Message}";
            }
            else if (ex.InnerException is NetGding.Contracts.Exceptions.NetGdingException inex)
            {
                errorMsg = $"❌ **Analysis Failed**\n• **Code:** `{inex.ErrorCode}`\n• **Location:** `{inex.Location}`\n• **Message:** {inex.Message}";
            }

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent(errorMsg))
                .ConfigureAwait(false);
        }
    }

    private async Task<AnalysisNotification> FetchOnDemandAnalysisAsync(
        string symbol,
        string timeframe,
        string exchange,
        string marketType,
        string? chartSymbol = null,
        bool chartOnly = false)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/analysis/on-demand";
        var payload = new { symbol, timeframe, exchange, marketType, chartSymbol, chartOnly };

        var response = await HttpRetryHelper.ExecuteAsync(
            () => _httpFactory.CreateClient("WebApiClient").PostAsJsonAsync(url, payload),
            maxRetries: Math.Max(1, o.OnDemandMaxRetries),
            baseDelaySeconds: o.OnDemandRetryBaseDelaySeconds,
            onRetry: (attempt, max, status) => _logger.LogWarning(
                "AnalysisCommands: on-demand attempt {Attempt}/{Max} failed (status={Status}) for {Symbol} ({Timeframe})",
                attempt, max, status, symbol, timeframe)).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }

            response.EnsureSuccessStatusCode();
        }

        var notification = await response.Content
            .ReadFromJsonAsync<AnalysisNotification>(s_jsonOptions)
            .ConfigureAwait(false);

        return notification ?? throw new InvalidOperationException("WebAPI returned empty response.");
    }

    [SlashCommand("fagi", "Get the current Crypto Fear and Greed Index")]
    public async Task FagiAsync(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        try
        {
            var fng = await FetchFearAndGreedAsync().ConfigureAwait(false);

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

    private async Task<FearAndGreedResult> FetchFearAndGreedAsync()
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/fear-and-greed";
        var response = await _httpFactory.CreateClient("WebApiClient").GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FearAndGreedResult>(s_jsonOptions).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("WebAPI returned empty response.");
    }

    private static DiscordColor GetFearAndGreedColor(int value) => value switch
    {
        <= 25 => new DiscordColor(0xD63031),
        <= 45 => new DiscordColor(0xE67E22),
        <= 55 => new DiscordColor(0xF1C40F),
        <= 75 => new DiscordColor(0x2ECC71),
        _ => new DiscordColor(0x00B894)
    };

    [SlashCommand("chart", "Get live chart for a symbol")]
    public async Task ChartAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC (default: BTC)")] string symbol = "BTC",
        [Option("timeframe", "Timeframe: 15m, 1h, 4h, 1d, 1w, 1m (default: 4h)")] string timeframe = "4h",
        [Option("exchange", "Exchange: binance, okx (default: binance)")] string exchange = "binance",
        [Option("market_type", "Market type: spot, future (default: spot)")] string marketType = "spot")
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        timeframe = timeframe.Trim().ToLowerInvariant();
        exchange = exchange.Trim().ToLowerInvariant();
        marketType = marketType.Trim().ToLowerInvariant();

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported exchanges: `binance`, `okx`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedMarketTypes.Contains(marketType))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported market types: `spot`, `future`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe, exchange, marketType, chartOnly: true)
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
            _logger.LogError(ex,
                "AnalysisCommands: on-demand chart failed for {Symbol} ({Timeframe})",
                normalizedSymbol, timeframe);

            var errorMsg = "Collector service is unavailable. Please try again in a moment.";
            if (ex is NetGding.Contracts.Exceptions.NetGdingException nex)
            {
                errorMsg = $"❌ **Chart Generation Failed**\n• **Code:** `{nex.ErrorCode}`\n• **Location:** `{nex.Location}`\n• **Message:** {nex.Message}";
            }
            else if (ex.InnerException is NetGding.Contracts.Exceptions.NetGdingException inex)
            {
                errorMsg = $"❌ **Chart Generation Failed**\n• **Code:** `{inex.ErrorCode}`\n• **Location:** `{inex.Location}`\n• **Message:** {inex.Message}";
            }

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent(errorMsg))
                .ConfigureAwait(false);
        }
    }

    [SlashCommand("news", "Get news articles for a symbol")]
    public async Task NewsAsync(
        InteractionContext ctx,
        [Option("symbol", "Symbol e.g. BTC (default: BTC)")] string symbol = "BTC",
        [Option("limit", "Number of articles (default: 5)")] long limit = 5)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normLimit = (int)Math.Clamp(limit, 1, 10);

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var articles = await FetchNewsAsync(normalizedSymbol, normLimit).ConfigureAwait(false);
            var embed = _formatter.BuildNewsEmbed(normalizedSymbol, articles);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(embed)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalysisCommands: news fetch failed for {Symbol}", normalizedSymbol);

            var errorMsg = "WebAPI news service is unavailable. Please try again in a moment.";
            if (ex is NetGding.Contracts.Exceptions.NetGdingException nex)
            {
                errorMsg = $"❌ **News Fetch Failed**\n• **Code:** `{nex.ErrorCode}`\n• **Location:** `{nex.Location}`\n• **Message:** {nex.Message}";
            }
            else if (ex.InnerException is NetGding.Contracts.Exceptions.NetGdingException inex)
            {
                errorMsg = $"❌ **News Fetch Failed**\n• **Code:** `{inex.ErrorCode}`\n• **Location:** `{inex.Location}`\n• **Message:** {inex.Message}";
            }

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent(errorMsg))
                .ConfigureAwait(false);
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

        if (!s_allowedTimeframes.Contains(timeframe))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported timeframes: `15m`, `1h`, `4h`, `1d`, `1w`, `1m`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedExchanges.Contains(exchange))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported exchanges: `binance`, `okx`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        if (!s_allowedMarketTypes.Contains(marketType))
        {
            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent("Supported market types: `spot`, `future`.")
                    .AsEphemeral(true)).ConfigureAwait(false);
            return;
        }

        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);

        try
        {
            var notification = await FetchOnDemandAnalysisAsync(normalizedSymbol, timeframe, exchange, marketType, "CRYPTOCAP:BTC.D", chartOnly: true).ConfigureAwait(false);

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

            var errorMsg = "Service is unavailable. Please try again in a moment.";
            if (ex is NetGding.Contracts.Exceptions.NetGdingException nex)
            {
                errorMsg = $"❌ **Request Failed**\n• **Code:** `{nex.ErrorCode}`\n• **Location:** `{nex.Location}`\n• **Message:** {nex.Message}";
            }
            else if (ex.InnerException is NetGding.Contracts.Exceptions.NetGdingException inex)
            {
                errorMsg = $"❌ **Request Failed**\n• **Code:** `{inex.ErrorCode}`\n• **Location:** `{inex.Location}`\n• **Message:** {inex.Message}";
            }

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                    .WithContent(errorMsg))
                .ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<DiscordNewsItem>> FetchNewsAsync(string symbol, int limit)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/news/{Uri.EscapeDataString(symbol)}?limit={limit}";

        var response = await _httpFactory.CreateClient("WebApiClient").GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content.ReadFromJsonAsync<DiscordNewsResponse>(s_jsonOptions).ConfigureAwait(false);
        return payload?.Items ?? Array.Empty<DiscordNewsItem>();
    }

    private async Task<MarketDepthDto?> FetchDomAsync(string symbol, string exchange, string marketType, int limit)
    {
        var o = _options.CurrentValue;
        var url = $"{o.WebApiBaseUrl.TrimEnd('/')}/api/market/dom?symbol={Uri.EscapeDataString(symbol)}&exchange={Uri.EscapeDataString(exchange)}&marketType={Uri.EscapeDataString(marketType)}&limit={limit}";

        var response = await _httpFactory.CreateClient("WebApiClient").GetAsync(url).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errResp = null;
            try
            {
                errResp = await response.Content.ReadFromJsonAsync<ErrorResponse>(s_jsonOptions).ConfigureAwait(false);
            }
            catch { }

            if (errResp is not null && !string.IsNullOrWhiteSpace(errResp.ErrorCode))
            {
                throw new NetGding.Contracts.Exceptions.NetGdingException(
                    errResp.ErrorCode,
                    errResp.Location,
                    errResp.Message);
            }
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MarketDepthDto>(s_jsonOptions).ConfigureAwait(false);
    }

    private static string NormalizeSymbol(string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return normalized.Contains('/', StringComparison.Ordinal) ? normalized : $"{normalized}/USD";
    }

    private sealed record DiscordNewsResponse(
        string Symbol,
        int Count,
        IReadOnlyList<DiscordNewsItem> Items);
}