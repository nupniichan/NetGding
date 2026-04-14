using DSharpPlus;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.Analysis;
using NetGding.Discord.Commands;
using NetGding.Discord.Endpoints;
using NetGding.Discord.Formatting;
using NetGding.Discord.Services;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DiscordOptions>()
    .BindConfiguration(DiscordOptions.SectionName);

builder.Services.AddHttpClient("WebApiClient", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;
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

builder.Services.AddSingleton<IAnalysisStore, AnalysisStore>();
builder.Services.AddSingleton<AnalysisEmbedFormatter>();
builder.Services.AddSingleton<IDiscordNotifier, DiscordNotifier>();
builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

app.MapNotifyEndpoints();

await app.RunAsync().ConfigureAwait(false);