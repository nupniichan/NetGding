using Alpaca.Markets;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Llm;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Endpoints;
using NetGding.Collector.Services;
using NetGding.Collector.Workers;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CollectorOptions>()
    .BindConfiguration(CollectorOptions.SectionName);

builder.Services.AddSingleton<IAlpacaDataClient>(sp =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    var key = new SecretKey(o.ApiKey, o.ApiSecret);
    return o.UsePaper
        ? Alpaca.Markets.Environments.Paper.GetAlpacaDataClient(key)
        : Alpaca.Markets.Environments.Live.GetAlpacaDataClient(key);
});

builder.Services.AddSingleton<IAlpacaCryptoDataClient>(sp =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    var key = new SecretKey(o.ApiKey, o.ApiSecret);
    return o.UsePaper
        ? Alpaca.Markets.Environments.Paper.GetAlpacaCryptoDataClient(key)
        : Alpaca.Markets.Environments.Live.GetAlpacaCryptoDataClient(key);
});

builder.Services.AddSingleton<IAlpacaOhlcvCollector, AlpacaOhlcvCollector>();
builder.Services.AddSingleton<IAlpacaNewsCollector, AlpacaNewsCollector>();

builder.Services.AddHttpClient(nameof(WebApiAnalysisPublisher), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.WebApiHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
        client.BaseAddress = new Uri(o.WebApiBaseUrl);
});
builder.Services.AddSingleton<IAnalysisPublisher, WebApiAnalysisPublisher>();

builder.Services.AddHttpClient<LlmAnalyzer>();
builder.Services.AddSingleton<ILlmAnalyzer>(sp =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    var llmOptions = Options.Create(new LlmOptions
    {
        BaseUrl = o.LlmBaseUrl,
        ApiKey = o.LlmApiKey,
        ModelName = o.LlmModel
    });
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<LlmAnalyzer>>();
    return new LlmAnalyzer(httpFactory.CreateClient(nameof(LlmAnalyzer)), llmOptions, logger);
});

builder.Services.AddSingleton<IOnDemandAnalyzer, OnDemandAnalyzer>();

builder.Services.AddHostedService<CollectorWorker>();
builder.Services.AddHostedService<NewsCollectorWorker>();
builder.Services.AddHostedService<AnalysisWorker>();

var app = builder.Build();

app.MapAnalysisEndpoints();

await app.RunAsync().ConfigureAwait(false);
