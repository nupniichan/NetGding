using NetGding.Configurations.Options;

namespace NetGding.Configurations.Bootstrap;

public static class EnvFileLoader
{
    private static readonly IReadOnlyDictionary<string, (string Section, string Property)> KeyMappings =
        new Dictionary<string, (string Section, string Property)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alpaca_ApiKey"] = (CollectorOptions.SectionName, nameof(CollectorOptions.ApiKey)),
            ["Alpaca_ApiSecret"] = (CollectorOptions.SectionName, nameof(CollectorOptions.ApiSecret)),
            ["Llm_ApiKey"] = (CollectorOptions.SectionName, nameof(CollectorOptions.LlmApiKey)),
            ["Llm_BaseUrl"] = (CollectorOptions.SectionName, nameof(CollectorOptions.LlmBaseUrl)),
            ["Llm_Model"] = (CollectorOptions.SectionName, nameof(CollectorOptions.LlmModel)),
            ["AnalysisPublish_WebApiBaseUrl"] = (CollectorOptions.SectionName, nameof(CollectorOptions.WebApiBaseUrl)),
            ["AnalysisPublish_Enabled"] = (CollectorOptions.SectionName, nameof(CollectorOptions.WebApiPublishEnabled)),
            ["MarketData_OutputDirectory"] = (CollectorOptions.SectionName, nameof(CollectorOptions.OutputDirectory)),
            ["Telegram_BotToken"] = (TelegramOptions.SectionName, nameof(TelegramOptions.BotToken)),
            ["Telegram_ChatId"] = (TelegramOptions.SectionName, nameof(TelegramOptions.ChatId)),
            ["WebApi_TelegramServiceUrl"] = (WebApiOptions.SectionName, nameof(WebApiOptions.TelegramServiceUrl)),
            ["WebApi_CollectorServiceUrl"] = (WebApiOptions.SectionName, nameof(WebApiOptions.CollectorServiceUrl)),
            ["WebApi_AnalyzerServiceUrl"] = (WebApiOptions.SectionName, nameof(WebApiOptions.AnalyzerServiceUrl)),
            ["WebApi_NewsServiceUrl"] = (WebApiOptions.SectionName, nameof(WebApiOptions.NewsServiceUrl)),
            ["WebApi_HealthProbePath"] = (WebApiOptions.SectionName, nameof(WebApiOptions.HealthProbePath))
        };

    public static void Load()
    {
        ApplyMappingsFromEnvironment();

        var envPath = FindDotEnvPath();
        if (envPath is null)
            return;

        foreach (var rawLine in File.ReadLines(envPath))
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

            SetIfMissing(key, value);

            if (KeyMappings.TryGetValue(key, out var target))
                SetIfMissing(BuildConfigurationKey(target.Section, target.Property), value);
        }
    }

    private static void ApplyMappingsFromEnvironment()
    {
        foreach (var (sourceKey, target) in KeyMappings)
        {
            var sourceValue = Environment.GetEnvironmentVariable(sourceKey);
            if (string.IsNullOrWhiteSpace(sourceValue))
                continue;

            SetIfMissing(BuildConfigurationKey(target.Section, target.Property), sourceValue);
        }
    }

    private static string BuildConfigurationKey(string section, string property) =>
        $"{section}__{property}";

    private static void SetIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }

    private static string? FindDotEnvPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }
}