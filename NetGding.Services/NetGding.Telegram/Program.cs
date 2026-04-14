using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Telegram.Endpoints;
using NetGding.Telegram.Formatting;
using NetGding.Telegram.Services;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<TelegramOptions>()
    .BindConfiguration(TelegramOptions.SectionName);

builder.Services.AddHttpClient(nameof(TelegramNotifier), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.NotifierHttpTimeoutSeconds);
});

builder.Services.AddHttpClient("WebApiClient", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.WebApiHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
        client.BaseAddress = new Uri(o.WebApiBaseUrl);
});

builder.Services.AddSingleton<IAnalysisStore, AnalysisStore>();
builder.Services.AddSingleton<AnalysisMessageFormatter>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddHostedService<BotPollingService>();

var app = builder.Build();

app.MapNotifyEndpoints();

await app.RunAsync().ConfigureAwait(false);