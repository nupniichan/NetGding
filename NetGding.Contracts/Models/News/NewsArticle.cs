namespace NetGding.Contracts.Models.News;

public sealed record NewsArticle(
    long Id,
    string Headline,
    string Author,
    string Source,
    string Summary,
    string Url,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<string> Symbols,
    string ImageUrl);