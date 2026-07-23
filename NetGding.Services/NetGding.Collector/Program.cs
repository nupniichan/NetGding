using Microsoft.Extensions.Options;
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

var sharedJsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Shared.json");
if (File.Exists(sharedJsonPath))
{
    builder.Configuration.AddJsonFile(sharedJsonPath, optional: true, reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<CollectorOptions>()
    .BindConfiguration(CollectorOptions.SectionName);

builder.Services.AddRedisMessaging(builder.Configuration);

builder.Services
    .AddOptions<LlmOptions>()
    .BindConfiguration(LlmOptions.SectionName);

builder.Services
    .AddOptions<SignalEngineOptions>()
    .BindConfiguration(SignalEngineOptions.SectionName);

builder.Services.AddHttpClient(nameof(BinanceMarketDataCollector));
builder.Services.AddHttpClient(nameof(OkxMarketDataCollector));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new BinanceMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<BinanceMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Spot));
builder.Services.AddSingleton<IExchangeMarketDataCollector>(sp =>
    new OkxMarketDataCollector(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<OkxMarketDataCollector>>(),
        NetGding.Contracts.Models.MarketData.MarketType.Spot));
builder.Services.AddSingleton<IMarketDataCollectorResolver, MarketDataCollectorResolver>();

// WebApiAnalysisPublisher removed: analysis results now returned directly in HTTP response
// (no longer needs to publish to Redis since we use direct HTTP call from WebAPI)

builder.Services.AddHttpClient(nameof(LlmAnalyzer), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(o.BaseUrl))
        client.BaseAddress = new Uri(o.BaseUrl);
    if (o.HttpTimeoutSeconds > 0)
        client.Timeout = TimeSpan.FromSeconds(o.HttpTimeoutSeconds);
});
builder.Services.AddSingleton<ILlmAnalyzer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LlmOptions>>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<LlmAnalyzer>>();
    return new LlmAnalyzer(httpFactory.CreateClient(nameof(LlmAnalyzer)), options, logger);
});

builder.Services.AddSingleton<ISignalEngine, SignalEngine>();
builder.Services.AddSingleton<IRiskCalculator, RiskCalculator>();

builder.Services.AddHttpClient(nameof(AnalysisChartRenderer), (sp, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "NetGding/1.0");
});
builder.Services.AddSingleton<IChartRenderer, AnalysisChartRenderer>();

// CachedMarketDataProvider: polls WebAPI HTTP endpoints for News/FnG every 15 min
// (replaces Redis stream subscription, breaking the circular dependency)
builder.Services.AddHttpClient(nameof(CachedMarketDataProvider));
builder.Services.AddSingleton<CachedMarketDataProvider>();
builder.Services.AddSingleton<ICachedMarketDataProvider>(sp => sp.GetRequiredService<CachedMarketDataProvider>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CachedMarketDataProvider>());

builder.Services.AddSingleton<IOnDemandAnalyzer, OnDemandAnalyzer>();
// OnDemandAnalysisWorker removed: analysis is now triggered via HTTP (WebAPI calls POST /api/analysis/on-demand directly)

var app = builder.Build();

app.MapAnalysisEndpoints();
app.MapMinimalHealthEndpoint();

await app.RunAsync().ConfigureAwait(false);