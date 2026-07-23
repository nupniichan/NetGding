namespace NetGding.Analyzer.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int HttpTimeoutSeconds { get; set; } = 60;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
}
