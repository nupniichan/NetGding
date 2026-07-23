using NetGding.Contracts.Models.News;

namespace NetGding.Contracts.Events;

public sealed record NewsDataEvent(
    string Symbol,
    IReadOnlyList<NewsArticle> Articles,
    DateTime FetchedAtUtc);
