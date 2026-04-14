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

    private readonly IOptionsMonitor<WebApiOptions> _options;
    private readonly ILogger<FileSystemNewsProvider> _logger;

    public FileSystemNewsProvider(
        IOptionsMonitor<WebApiOptions> options,
        ILogger<FileSystemNewsProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(
        string symbol,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var o = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(o.OutputDirectory))
            return [];

        var safeSymbol = symbol.Trim().Replace('/', '_').Replace('\\', '_');
        var symbolDirectory = Path.Combine(o.OutputDirectory, safeSymbol);
        if (!Directory.Exists(symbolDirectory))
            return [];

        var newsMaxLimit = o.NewsMaxLimit;
        var files = Directory.EnumerateFiles(symbolDirectory, "news_*.json")
            .OrderByDescending(x => x)
            .ToArray();

        var results = new List<NewsItemDto>(Math.Min(limit, newsMaxLimit));
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
