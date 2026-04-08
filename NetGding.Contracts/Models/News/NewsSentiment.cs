namespace NetGding.Contracts.Models.News;

public sealed class NewsSentiment
{
    public required string Symbol { get; init; }
    public int ArticleCount { get; set; }
    public float OverallScore { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
    public DateTime AnalyzedAtUtc { get; set; }

    public Dictionary<string, float> ArticleScores { get; set; } = new();
}