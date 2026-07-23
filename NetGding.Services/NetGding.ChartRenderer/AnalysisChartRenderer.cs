using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;
using NetGding.Contracts.Models.Analysis.Enums;
using NetGding.Contracts.Models.MarketData;

namespace NetGding.ChartRenderer;

public sealed class AnalysisChartRenderer : IChartRenderer
{
    private const string ApiUrl = "https://api.chart-img.com/v2/tradingview/advanced-chart";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CollectorOptions> _options;
    private readonly ILogger<AnalysisChartRenderer> _logger;

    public AnalysisChartRenderer(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CollectorOptions> options,
        ILogger<AnalysisChartRenderer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(
        IReadOnlyList<OhlcvBar> bars,
        AnalysisResult result,
        string exchange,
        CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            _logger.LogWarning("AnalysisChartRenderer: empty bar list, skipping chart rendering");
            return [];
        }

        var opt = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opt.ChartImgApiKey))
        {
            throw new NetGdingException(
                ErrorCodes.ChartRenderFailed,
                "AnalysisChartRenderer.RenderAsync",
                "ChartImgApiKey is not configured.");
        }

        try
        {
            var tvSymbol = !string.IsNullOrWhiteSpace(result.ChartSymbol)
                ? result.ChartSymbol
                : FormatTradingViewSymbol(exchange, result.Symbol);
            var tvInterval = NormalizeTimeframe(result.Timeframe);

            var studiesList = new List<object>();
            if (string.IsNullOrWhiteSpace(result.ChartSymbol))
            {
                studiesList.Add(new { name = "Volume" });
            }

            var potentialDrawings = new List<object>();

            if (string.IsNullOrWhiteSpace(result.ChartSymbol))
            {
                var supports = new List<dynamic>();
                var resistances = new List<dynamic>();

                foreach (var (key, val) in result.Indicators.SupportResistance)
                {
                    bool isSupport = key.StartsWith('S');
                    var distance = Math.Abs((double)result.CurrentPrice - (double)val);
                    var item = new
                    {
                        Distance = distance,
                        Key = key,
                        Val = (double)val,
                        IsSupport = isSupport
                    };

                    if (isSupport)
                        supports.Add(item);
                    else
                        resistances.Add(item);
                }

                var closestSupport = supports.OrderBy(x => (double)x.Distance).FirstOrDefault();
                var closestResistance = resistances.OrderBy(x => (double)x.Distance).FirstOrDefault();

                if (closestSupport != null)
                {
                    potentialDrawings.Add(CreateHorizontalLineDrawing(closestSupport.Key, closestSupport.Val, true));
                }
                if (closestResistance != null)
                {
                    potentialDrawings.Add(CreateHorizontalLineDrawing(closestResistance.Key, closestResistance.Val, false));
                }

                var remainingSr = supports.Concat(resistances)
                    .Where(x => x != closestSupport && x != closestResistance)
                    .OrderBy(x => (double)x.Distance);

                foreach (var item in remainingSr)
                {
                    potentialDrawings.Add(CreateHorizontalLineDrawing(item.Key, item.Val, item.IsSupport));
                }
            }

            int maxDrawings = Math.Max(0, 3 - studiesList.Count);
            var drawingsList = potentialDrawings.Take(maxDrawings).ToList();

            var payload = new
            {
                symbol = tvSymbol,
                interval = tvInterval,
                width = 800,
                height = 500,
                theme = "dark",
                studies = studiesList,
                drawings = drawingsList
            };

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("x-api-key", opt.ChartImgApiKey);

            var client = _httpClientFactory.CreateClient(nameof(AnalysisChartRenderer));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("AnalysisChartRenderer: API request failed with status code {StatusCode}: {Body}",
                    (int)response.StatusCode, errorBody);

                throw new NetGdingException(
                    ErrorCodes.ChartRenderFailed,
                    "AnalysisChartRenderer.RenderAsync",
                    $"Chart-Img API request failed with status code {(int)response.StatusCode}: {errorBody}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not NetGdingException)
        {
            _logger.LogError(ex, "AnalysisChartRenderer: failed to render chart for {Symbol}", result.Symbol);
            throw new NetGdingException(
                ErrorCodes.ChartRenderFailed,
                "AnalysisChartRenderer.RenderAsync",
                $"Failed to render chart for {result.Symbol}: {ex.Message}", ex);
        }
    }

    private static object CreateHorizontalLineDrawing(string key, double val, bool isSupport)
    {
        return new
        {
            name = "Horizontal Line",
            input = new
            {
                price = val,
                text = key
            },
            @override = new
            {
                lineColor = isSupport ? "rgba(38,166,154,0.8)" : "rgba(239,83,80,0.8)",
                lineWidth = 1,
                lineStyle = 2
            }
        };
    }

    private static string FormatTradingViewSymbol(string exchange, string symbol)
    {
        var upperExchange = exchange.Trim().ToUpperInvariant();
        var upperSymbol = symbol.Trim().ToUpperInvariant().Replace("/", "").Replace("-", "");

        return $"{upperExchange}:{upperSymbol}";
    }

    private static string NormalizeTimeframe(string timeframe)
    {
        return timeframe.Trim().ToLowerInvariant() switch
        {
            "15m" or "15min" => "15m",
            "1h" or "1hour" => "1h",
            "4h" or "4hour" => "4h",
            "1d" or "1day" or "d" => "1D",
            "1w" or "1week" or "w" => "1W",
            "1m" or "1month" or "mo" => "1M",
            _ => "1D"
        };
    }
}
