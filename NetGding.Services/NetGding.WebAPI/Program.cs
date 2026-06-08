using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Bootstrap;
using NetGding.Configurations.Options;
using NetGding.WebApi.Endpoints;
using NetGding.WebApi.Persistence;
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
builder.Services.AddDbContext<TradingDbContext>((sp, options) =>
{
    var o = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    var connectionString = o.ConnectionString;

    if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    {
        var connBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var dataSource = connBuilder.DataSource;
        if (!string.IsNullOrEmpty(dataSource) && !Path.IsPathRooted(dataSource))
        {
            var rootDir = Environment.GetEnvironmentVariable("NETGDING_ROOT_DIR") ?? AppContext.BaseDirectory;
            var fullPath = Path.GetFullPath(Path.Combine(rootDir, dataSource));

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            connBuilder.DataSource = fullPath;
            connectionString = connBuilder.ToString();
        }
    }

    options.UseSqlite(connectionString);
});
builder.Services.AddScoped<IAnalysisResultStore, SqliteAnalysisResultStore>();
builder.Services.AddSingleton<ISymbolMetadataProvider, SymbolMetadataProvider>();
builder.Services.AddHttpClient<INewsProvider, AlphaVantageNewsProvider>();
builder.Services.AddHttpClient<IFearAndGreedProvider, CoinMarketCapFearAndGreedProvider>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "NetGding WebAPI", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
    dbContext.Database.EnsureCreated();

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE AnalysisResults ADD COLUMN FearAndGreedIndex INTEGER NULL;");
    }
    catch (Exception) { }

    try
    {
        dbContext.Database.ExecuteSqlRaw("ALTER TABLE AnalysisResults ADD COLUMN FearAndGreedClassification TEXT NULL;");
    }
    catch (Exception) { }
}

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
app.MapSentimentEndpoints();

await app.RunAsync().ConfigureAwait(false);