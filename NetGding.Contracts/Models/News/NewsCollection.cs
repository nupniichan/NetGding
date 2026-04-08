namespace NetGding.Contracts.Models.News;

public sealed record NewsCollection(
    string Symbol,
    IReadOnlyList<NewsArticle> Articles);