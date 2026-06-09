namespace NetGding.Analyzer.FinBert;

public sealed record SentimentPrediction(
    string Text,
    SentimentLabel Label,
    float Score);
