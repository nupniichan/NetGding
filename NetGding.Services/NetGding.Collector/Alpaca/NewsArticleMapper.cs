using Alpaca.Markets;
using NetGding.Contracts.Models.News;

namespace NetGding.Collector.Alpaca;

internal static class NewsArticleMapper
{
    public static NewsArticle FromAlpaca(INewsArticle a)
    {
        var imageUrl = string.Empty;
        try
        {
            var images = a.SmallImageUrl;
            if (images is not null)
                imageUrl = images.ToString();
        }
        catch
        {

        }

        return new NewsArticle(
            Id: a.Id,
            Headline: a.Headline ?? string.Empty,
            Author: a.Author ?? string.Empty,
            Source: a.Source ?? string.Empty,
            Summary: a.Summary ?? string.Empty,
            Url: a.ArticleUrl?.ToString() ?? string.Empty,
            CreatedAtUtc: a.CreatedAtUtc,
            UpdatedAtUtc: a.UpdatedAtUtc,
            Symbols: a.Symbols?.ToList().AsReadOnly()
                     ?? (IReadOnlyList<string>)Array.Empty<string>(),
            ImageUrl: imageUrl);
    }
}