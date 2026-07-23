using NetGding.Contracts.Models.Analysis;

namespace NetGding.Contracts.Events;

public sealed record AnalysisResponseEvent(
    string RequestId,
    bool Success,
    AnalysisNotification? Notification,
    string? ErrorCode,
    string? ErrorLocation,
    string? ErrorMessage,
    DateTime RespondedAtUtc);
