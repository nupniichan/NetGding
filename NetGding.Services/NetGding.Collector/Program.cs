using Microsoft.Extensions.Options;
using NetGding.Analyzer.FinBert;
using NetGding.Analyzer.Llm;
using NetGding.Analyzer.Signal;
using NetGding.ChartRenderer;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Collector.Endpoints;
using NetGding.Collector.Services;
using NetGding.Collector.Services.MarketData;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CollectorOptions>()
    .BindConfiguration(CollectorOptions.SectionName);

builder.Services
    .AddOptions<LlmOptions>()
    .BindConfiguration(LlmOptions.SectionName);

builder.Services
    .AddOptions<SignalEngineOptions>()
    .BindConfiguration(SignalEngineOptions.SectionName);

builder.Services
    .AddOptions<FinBertOptions>()
    .BindConfiguration(FinBertOptions.SectionName);

builder.Services.AddHttpClient(nameof(BinanceMarketDataCollector));
builder.Services.AddHttpClient(nameof(OkxMarketDataCollector));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new BinanceMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<BinanceMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Spot));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new BinanceMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<BinanceMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Future));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new OkxMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<OkxMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Spot));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new OkxMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<OkxMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Future));
builder.Services.AddSingleton<IMarketDataCollectorResolver, MarketDataCollectorResolver>();

builder.Services.AddHttpClient(nameof(WebApiAnalysisPublisher), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.WebApiHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
        client.BaseAddress = new Uri(o.WebApiBaseUrl);
});
builder.Services.AddSingleton<IAnalysisPublisher, WebApiAnalysisPublisher>();

builder.Services.AddHttpClient(nameof(LlmAnalyzer), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(o.BaseUrl))
        client.BaseAddress = new Uri(o.BaseUrl);
});
builder.Services.AddHttpClient(nameof(FinBertSentimentAnalyzer));
builder.Services.AddSingleton<IFinBertSentimentAnalyzer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<FinBertOptions>>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<FinBertSentimentAnalyzer>>();
    return new FinBertSentimentAnalyzer(httpFactory.CreateClient(nameof(FinBertSentimentAnalyzer)), options, logger);
});

builder.Services.AddSingleton<ILlmAnalyzer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LlmOptions>>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var sentimentAnalyzer = sp.GetRequiredService<IFinBertSentimentAnalyzer>();
    var logger = sp.GetRequiredService<ILogger<LlmAnalyzer>>();
    return new LlmAnalyzer(httpFactory.CreateClient(nameof(LlmAnalyzer)), options, sentimentAnalyzer, logger);
});

builder.Services.AddSingleton<ISignalEngine, SignalEngine>();
builder.Services.AddSingleton<IRiskCalculator, RiskCalculator>();

builder.Services.AddHttpClient(nameof(AnalysisChartRenderer));
builder.Services.AddSingleton<IChartRenderer, AnalysisChartRenderer>();

builder.Services.AddSingleton<IOnDemandAnalyzer, OnDemandAnalyzer>();

var app = builder.Build();

app.MapAnalysisEndpoints();

await app.RunAsync().ConfigureAwait(false);