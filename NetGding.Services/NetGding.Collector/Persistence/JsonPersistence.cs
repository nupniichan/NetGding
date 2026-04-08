using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NetGding.Collector.Persistence;

public static class JsonPersistence
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync<T>(
        string outputDirectory,
        string symbol,
        string dataType,
        T data,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return;

        try
        {
            var safeSymbol = symbol.Replace('/', '_').Replace('\\', '_');
            var dir = Path.Combine(outputDirectory, safeSymbol);
            Directory.CreateDirectory(dir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{dataType}_{timestamp}.json";
            var filePath = Path.Combine(dir, fileName);

            var json = JsonSerializer.Serialize(data, s_options);
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

            logger?.LogDebug("Persisted {DataType} for {Symbol} → {Path}",
                dataType, symbol, filePath);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to persist {DataType} for {Symbol}",
                dataType, symbol);
        }
    }
}