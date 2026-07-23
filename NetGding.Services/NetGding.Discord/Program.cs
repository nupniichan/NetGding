using DSharpPlus;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Services;
using NetGding.Discord.Formatting;
using NetGding.Discord.Services;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

var sharedJsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Shared.json");
if (File.Exists(sharedJsonPath))
{
    builder.Configuration.AddJsonFile(sharedJsonPath, optional: true, reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<DiscordOptions>()
    .BindConfiguration(DiscordOptions.SectionName);

builder.Services.AddRedisMessaging(builder.Configuration);

builder.Services.AddHttpClient("WebApiClient", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
    if (o.WebApiHttpTimeoutSeconds > 0)
        client.Timeout = TimeSpan.FromSeconds(o.WebApiHttpTimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(o.WebApiBaseUrl))
        client.BaseAddress = new Uri(o.WebApiBaseUrl);
});

builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
    return new DiscordClient(new DiscordConfiguration
    {
        Token = string.IsNullOrWhiteSpace(o.BotToken) ? "placeholder" : o.BotToken,
        TokenType = TokenType.Bot,
        Intents = DiscordIntents.AllUnprivileged,
        LoggerFactory = sp.GetRequiredService<ILoggerFactory>()
    });
});

builder.Services.AddSingleton<IAnalysisCache, RedisAnalysisCache>();
builder.Services.AddSingleton<AnalysisEmbedFormatter>();
builder.Services.AddSingleton<IDiscordNotifier, DiscordNotifier>();
builder.Services.AddHostedService<DiscordBotService>();
builder.Services.AddHostedService<DiscordAnalysisSubscriberWorker>();

var app = builder.Build();

app.MapMinimalHealthEndpoint();

await app.RunAsync().ConfigureAwait(false);