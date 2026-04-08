using Alpaca.Markets;
using Microsoft.Extensions.Options;
using NetGding.Analyzer.Gemma;
using NetGding.Collector.Alpaca;
using NetGding.Collector.Configuration;
using NetGding.Collector.Workers;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddHttpClient<GemmaAnalyzer>();
builder.Services.AddSingleton<IGemmaAnalyzer>(sp =>
{
    var o = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    var gemmaOptions = Options.Create(new GemmaOptions
    {
        BaseUrl = o.GemmaBaseUrl,
        ApiKey = o.GemmaApiKey,
        ModelName = o.GemmaModel
    });
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<GemmaAnalyzer>>();
    return new GemmaAnalyzer(httpFactory.CreateClient(nameof(GemmaAnalyzer)), gemmaOptions, logger);
});

builder.Services.AddHostedService<CollectorWorker>();
builder.Services.AddHostedService<NewsCollectorWorker>();
builder.Services.AddHostedService<AnalysisWorker>();

await builder.Build().RunAsync().ConfigureAwait(false);