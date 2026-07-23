using NetGding.Configurations.Options;

namespace NetGding.Configurations.Bootstrap;

public sealed class EnvFileLoader
{
    private record KeyMapping(string SourceKey, string Section, string Property);

    private static readonly KeyMapping[] KeyMappings =
    [
        // Shared / Global System Configuration
        new("Llm_ApiKey", "Llm", "ApiKey"),
        new("Llm_BaseUrl", "Llm", "BaseUrl"),
        new("Llm_Model", "Llm", "ModelName"),
        new("Llm_HttpTimeoutSeconds", "Llm", "HttpTimeoutSeconds"),
        new("ChartEnabled", CollectorOptions.SectionName, nameof(CollectorOptions.ChartEnabled)),
        new("ChartImg_ApiKey", CollectorOptions.SectionName, nameof(CollectorOptions.ChartImgApiKey)),
        new("WebApi_BaseUrl", CollectorOptions.SectionName, nameof(CollectorOptions.WebApiBaseUrl)),
        new("WebApi_BaseUrl", TelegramOptions.SectionName, nameof(TelegramOptions.WebApiBaseUrl)),
        new("WebApi_BaseUrl", DiscordOptions.SectionName, nameof(DiscordOptions.WebApiBaseUrl)),
        new("Collector_ServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.CollectorServiceUrl)),
        new("Telegram_ServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.TelegramServiceUrl)),
        new("Discord_ServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.DiscordServiceUrl)),

        // Legacy / Service-Specific Overrides
        new("AnalysisPublish_WebApiBaseUrl", CollectorOptions.SectionName, nameof(CollectorOptions.WebApiBaseUrl)),
        new("AnalysisPublish_Enabled", CollectorOptions.SectionName, nameof(CollectorOptions.WebApiPublishEnabled)),
        new("MarketData_OutputDirectory", CollectorOptions.SectionName, nameof(CollectorOptions.OutputDirectory)),
        new("Telegram_BotToken", TelegramOptions.SectionName, nameof(TelegramOptions.BotToken)),
        new("Telegram_ChatId", TelegramOptions.SectionName, nameof(TelegramOptions.ChatId)),
        new("Discord_BotToken", DiscordOptions.SectionName, nameof(DiscordOptions.BotToken)),
        new("Discord_ChannelId", DiscordOptions.SectionName, nameof(DiscordOptions.ChannelId)),
        new("Discord_GuildId", DiscordOptions.SectionName, nameof(DiscordOptions.GuildId)),
        new("WebApi_TelegramServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.TelegramServiceUrl)),
        new("WebApi_CollectorServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.CollectorServiceUrl)),
        new("WebApi_AnalyzerServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.AnalyzerServiceUrl)),
        new("WebApi_NewsServiceUrl", WebApiOptions.SectionName, nameof(WebApiOptions.NewsServiceUrl)),
        new("WebApi_HealthProbePath", WebApiOptions.SectionName, nameof(WebApiOptions.HealthProbePath)),
        new("WebApi_ConnectionString", WebApiOptions.SectionName, nameof(WebApiOptions.ConnectionString)),
        new("AlphaVantage_ApiKey", WebApiOptions.SectionName, nameof(WebApiOptions.AlphaVantageApiKey)),
        new("CoinMarketCap_ApiKey", WebApiOptions.SectionName, nameof(WebApiOptions.CoinMarketCapApiKey))
    ];

    public async Task ReadEnvFile()
    {
        ApplyMappingsFromEnvironment();

        var rootDir = FindRootDirectory();
        if (rootDir is null)
            return;

        Environment.SetEnvironmentVariable("NETGDING_ROOT_DIR", rootDir);

        await LoadRootSharedJsonFileAsync(rootDir).ConfigureAwait(false);

        var envPath = Path.Combine(rootDir, ".env");
        if (!File.Exists(envPath))
            return;

        var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var sr = new StreamReader(envPath);
        string? rawLine;
        while ((rawLine = await sr.ReadLineAsync()) != null)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            envVars[key] = value;
        }

        foreach (var (key, value) in envVars)
        {
            SetIfMissing(key, value);

            foreach (var mapping in KeyMappings)
            {
                if (string.Equals(mapping.SourceKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    SetIfMissing(BuildConfigurationKey(mapping.Section, mapping.Property), value);
                }
            }
        }
    }

    private static void ApplyMappingsFromEnvironment()
    {
        foreach (var mapping in KeyMappings)
        {
            var sourceValue = Environment.GetEnvironmentVariable(mapping.SourceKey);
            if (string.IsNullOrWhiteSpace(sourceValue))
                continue;

            SetIfMissing(BuildConfigurationKey(mapping.Section, mapping.Property), sourceValue);
        }
    }

    private static async Task LoadRootSharedJsonFileAsync(string rootDir)
    {
        var sharedJsonPath = Path.Combine(rootDir, "appsettings.Shared.json");
        if (!File.Exists(sharedJsonPath))
        {
            sharedJsonPath = Path.Combine(rootDir, "appsettings.json");
            if (!File.Exists(sharedJsonPath))
                return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(sharedJsonPath).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            FlattenAndSetIfMissing(doc.RootElement, prefix: "");
        }
        catch
        {
            // Ignore syntax errors in root json
        }
    }

    private static void FlattenAndSetIfMissing(System.Text.Json.JsonElement element, string prefix)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}__{prop.Name}";
                    FlattenAndSetIfMissing(prop.Value, key);
                }
                break;
            case System.Text.Json.JsonValueKind.String:
            case System.Text.Json.JsonValueKind.Number:
            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                if (!string.IsNullOrEmpty(prefix))
                {
                    SetIfMissing(prefix, element.ToString());
                }
                break;
        }
    }

    private static string BuildConfigurationKey(string section, string property) =>
        $"{section}__{property}";

    private static void SetIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }

    private static string? FindRootDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".env")) ||
                File.Exists(Path.Combine(current.FullName, "appsettings.Shared.json")) ||
                File.Exists(Path.Combine(current.FullName, "NetGding.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}