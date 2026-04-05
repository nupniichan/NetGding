using Alpaca.Markets;
using Microsoft.Extensions.Options;
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

builder.Services.AddSingleton<IAlpacaOhlcvCollector, AlpacaOhlcvCollector>();
builder.Services.AddHostedService<CollectorWorker>();

await builder.Build().RunAsync().ConfigureAwait(false);