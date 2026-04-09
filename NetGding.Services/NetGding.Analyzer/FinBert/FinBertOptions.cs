namespace NetGding.Analyzer.FinBert;

public sealed class FinBertOptions
{
    public const string SectionName = "FinBert";

    public string InferenceUrl { get; set; } = "https://api-inference.huggingface.co/models/ProsusAI/finbert";
}