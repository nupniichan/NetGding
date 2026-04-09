namespace NetGding.Analyzer.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "google/gemma-4-26b-a4b-it:free";
    public int MaxAttempts { get; set; } = 3;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
}
