namespace NetGding.Contracts.Models.Analysis;

public sealed record ErrorResponse(
    string ErrorCode,
    string Location,
    string Message);
