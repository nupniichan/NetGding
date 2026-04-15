namespace NetGding.Contracts.Models.Analysis;

public sealed class AnalysisNotification
{
    public required AnalysisResult Result { get; set; }
    public string? ChartImageBase64 { get; set; }
}
