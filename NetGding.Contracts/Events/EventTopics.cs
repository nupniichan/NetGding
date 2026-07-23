namespace NetGding.Contracts.Events;

public static class EventTopics
{
    /// <summary>Fan-out: Collector → WebAPI (persist) + Telegram + Discord (notify)</summary>
    public const string AnalysisCompleted = "stream:analysis:completed";

    // NOTE: AnalysisRequest and AnalysisResponse topics removed.
    // Analysis is now triggered via direct HTTP: WebAPI → POST /api/analysis/on-demand → Collector.
    // Redis is used only for fan-out (AnalysisCompleted), not for request/response RPC.
}
