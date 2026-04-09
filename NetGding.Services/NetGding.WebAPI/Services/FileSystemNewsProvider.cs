using System.Text.Json;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.Contracts.Models.News;
using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public sealed class FileSystemNewsProvider : INewsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOptionsMonitor<CollectorOptions> _collectorOptions;
    private readonly ILogger<FileSystemNewsProvider> _logger;

    public FileSystemNewsProvider(
        IOptionsMonitor<CollectorOptions> collectorOptions,
        ILogger<FileSystemNewsProvider> logger)
    {
        _collectorOptions = collectorOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var outputDirectory = _collectorOptions.CurrentValue.OutputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return [];

        var safeSymbol = symbol.Trim().Replace('/', '_').Replace('\\', '_');
        var symbolDirectory = Path.Combine(outputDirectory, safeSymbol);
        if (!Directory.Exists(symbolDirectory))
            return [];

        var files = Directory.EnumerateFiles(symbolDirectory, "news_*.json")
            .OrderByDescending(x => x)
            .ToArray();

        var results = new List<NewsItemDto>(Math.Min(limit, 200));
        foreach (var file in files)
        {
            if (results.Count >= limit)
                break;

            try
            {
                await using var stream = File.OpenRead(file);
                var collection = await JsonSerializer.DeserializeAsync<NewsCollection>(stream, JsonOptions, ct)
                    .ConfigureAwait(false);
                if (collection is null || collection.Articles.Count == 0)
                    continue;

                foreach (var article in collection.Articles.OrderByDescending(x => x.UpdatedAtUtc))
                {
                    if (fromUtc.HasValue && article.UpdatedAtUtc < fromUtc.Value)
                        continue;
                    if (toUtc.HasValue && article.UpdatedAtUtc > toUtc.Value)
                        continue;

                    results.Add(new NewsItemDto(
                        article.Id,
                        symbol,
                        article.Headline,
                        article.Source,
                        article.Url,
                        article.UpdatedAtUtc,
                        article.Summary));

                    if (results.Count >= limit)
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read news file {File}", file);
            }
        }

        return results
            .OrderByDescending(x => x.PublishedAtUtc)
            .Take(limit)
            .ToArray();
    }
}
