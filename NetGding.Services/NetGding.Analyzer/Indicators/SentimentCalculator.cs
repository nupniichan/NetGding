using NetGding.Analyzer.FinBert;
using NetGding.Contracts.Models.News;

namespace NetGding.Analyzer.Indicators;

public static class SentimentCalculator
{
    public static async Task FillSentimentAsync(
        NewsSentiment target,
        IReadOnlyList<NewsArticle> articles,
        IFinBertSentimentAnalyzer analyzer,
        CancellationToken cancellationToken = default)
    {
        target.ArticleScores.Clear();
        target.PositiveCount = 0;
        target.NegativeCount = 0;
        target.NeutralCount = 0;

        if (articles.Count == 0)
        {
            target.ArticleCount = 0;
            target.OverallScore = 0f;
            target.AnalyzedAtUtc = DateTime.UtcNow;
            return;
        }

        var headlines = articles
            .Select(a => a.Headline)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        float totalScore = 0f;
        int scored = 0;

        foreach (var headline in headlines)
        {
            var pred = await analyzer
                .AnalyzeAsync(headline, cancellationToken)
                .ConfigureAwait(false);

            float signedScore = pred.Label switch
            {
                SentimentLabel.Positive => pred.Score,
                SentimentLabel.Negative => -pred.Score,
                _ => 0f
            };

            totalScore += signedScore;
            scored++;

            switch (pred.Label)
            {
                case SentimentLabel.Positive:
                    target.PositiveCount++;
                    break;
                case SentimentLabel.Negative:
                    target.NegativeCount++;
                    break;
                default:
                    target.NeutralCount++;
                    break;
            }

            var key = pred.Text.Length > 80 ? pred.Text[..80] : pred.Text;
            target.ArticleScores[key] = signedScore;
        }

        target.ArticleCount = scored;
        target.OverallScore = scored > 0 ? totalScore / scored : 0f;
        target.AnalyzedAtUtc = DateTime.UtcNow;
    }
}