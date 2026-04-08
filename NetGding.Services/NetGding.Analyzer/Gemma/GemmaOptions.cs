namespace NetGding.Analyzer.Gemma;

public sealed class GemmaOptions
{
    public const string SectionName = "Gemma";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "google/gemma-4-26b-a4b-it:free";
}