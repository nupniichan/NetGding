namespace NetGding.Analyzer.FinBert;

public interface IFinBertSentimentAnalyzer
{
    Task<SentimentPrediction> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default);
}