namespace NetGding.Analyzer.FinBert;

public enum SentimentLabel
{
    Positive,
    Negative,
    Neutral
}

public sealed record SentimentPrediction(
    string Text,
    SentimentLabel Label,
    float Score);

public interface IFinBertSentimentAnalyzer
{
    Task<SentimentPrediction> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default);
}