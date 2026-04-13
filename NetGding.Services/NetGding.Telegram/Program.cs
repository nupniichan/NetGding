using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
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

builder.Services.AddHttpClient("CollectorClient", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.CollectorHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.CollectorBaseUrl))
        client.BaseAddress = new Uri(o.CollectorBaseUrl);
});

builder.Services.AddSingleton<IAnalysisStore, AnalysisStore>();
builder.Services.AddSingleton<AnalysisMessageFormatter>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddHostedService<BotPollingService>();

var app = builder.Build();

app.MapNotifyEndpoints();

await app.RunAsync().ConfigureAwait(false);