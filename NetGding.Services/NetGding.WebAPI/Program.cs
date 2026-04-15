using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.WebApi.Endpoints;
using NetGding.WebApi.Services;

await new EnvFileLoader().ReadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<WebApiOptions>()
    .BindConfiguration(WebApiOptions.SectionName);

builder.Services.AddHttpClient(nameof(TelegramForwarder), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
});

builder.Services.AddHttpClient(nameof(DiscordForwarder), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
});

builder.Services.AddHttpClient(nameof(CollectorGateway), (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.CollectorTimeoutSeconds);
});

builder.Services.AddHttpClient("HealthProbe", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.HealthTimeoutSeconds);
});

builder.Services.AddSingleton<ITelegramForwarder, TelegramForwarder>();
builder.Services.AddSingleton<IDiscordForwarder, DiscordForwarder>();
builder.Services.AddSingleton<ICollectorGateway, CollectorGateway>();
builder.Services.AddSingleton<IAnalysisResultStore, InMemoryAnalysisResultStore>();
builder.Services.AddSingleton<ISymbolMetadataProvider, SymbolMetadataProvider>();
builder.Services.AddSingleton<INewsProvider, FileSystemNewsProvider>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "NetGding WebAPI", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NetGding WebAPI v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapAnalysisEndpoints();
app.MapSupportEndpoints();
app.MapHealthEndpoints();
app.MapIndicatorEndpoints();
app.MapNewsEndpoints();

await app.RunAsync().ConfigureAwait(false);