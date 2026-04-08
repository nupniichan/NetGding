using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NetGding.Collector.Persistence;

public static class JsonLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<T?> LoadLatestAsync<T>(
        string outputDirectory,
        string symbol,
        string dataType,
        ILogger? logger = null) where T : class
    {
        var json = await LoadLatestRawAsync(outputDirectory, symbol, dataType, logger)
            .ConfigureAwait(false);

        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, s_options);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to deserialize {DataType} for {Symbol}", dataType, symbol);
            return null;
        }
    }

    public static async Task<T?> LoadLatestStructAsync<T>(
        string outputDirectory,
        string symbol,
        string dataType,
        ILogger? logger = null) where T : struct
    {
        var json = await LoadLatestRawAsync(outputDirectory, symbol, dataType, logger)
            .ConfigureAwait(false);

        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, s_options);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Failed to deserialize {DataType} for {Symbol}", dataType, symbol);
            return null;
        }
    }

    private static async Task<string?> LoadLatestRawAsync(
        string outputDirectory,
        string symbol,
        string dataType,
        ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return null;

        try
        {
            var safeSymbol = symbol.Replace('/', '_').Replace('\\', '_');
            var dir = Path.Combine(outputDirectory, safeSymbol);

            if (!Directory.Exists(dir))
                return null;

            var pattern = $"{dataType}_*.json";
            var latest = Directory.EnumerateFiles(dir, pattern)
                .OrderDescending()
                .FirstOrDefault();

            if (latest is null)
                return null;

            return await File.ReadAllTextAsync(latest).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to load latest {DataType} for {Symbol}", dataType, symbol);
            return null;
        }
    }
}