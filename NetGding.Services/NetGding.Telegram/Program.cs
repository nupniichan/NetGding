using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Services;
using NetGding.Telegram.Formatting;
using NetGding.Telegram.Services;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

var sharedJsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Shared.json");
if (File.Exists(sharedJsonPath))
{
    builder.Configuration.AddJsonFile(sharedJsonPath, optional: true, reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<TelegramOptions>()
    .BindConfiguration(TelegramOptions.SectionName);

builder.Services.AddRedisMessaging(builder.Configuration);

builder.Services.AddHttpClient(nameof(TelegramNotifier), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    if (o.NotifierHttpTimeoutSeconds > 0)
        client.Timeout = TimeSpan.FromSeconds(o.NotifierHttpTimeoutSeconds);
});

builder.Services.AddHttpClient<IWebApiClient, WebApiClient>((sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    if (o.WebApiHttpTimeoutSeconds > 0)
        client.Timeout = TimeSpan.FromSeconds(o.WebApiHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
        client.BaseAddress = new Uri(o.WebApiBaseUrl.TrimEnd('/') + "/");
});

builder.Services.AddSingleton<IAnalysisCache, RedisAnalysisCache>();
builder.Services.AddSingleton<AnalysisMessageFormatter>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddHostedService<BotPollingService>();
builder.Services.AddHostedService<TelegramAnalysisSubscriberWorker>();

var app = builder.Build();

app.MapMinimalHealthEndpoint();

await app.RunAsync().ConfigureAwait(false);