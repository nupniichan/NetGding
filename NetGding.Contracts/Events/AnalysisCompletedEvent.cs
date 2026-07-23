using NetGding.Contracts.Models.Analysis;

namespace NetGding.Contracts.Events;

public sealed record AnalysisCompletedEvent(
    string EventId,
    DateTime OccurredAtUtc,
    AnalysisNotification Notification,
    string SourceService,
    string? RequestId = null);
