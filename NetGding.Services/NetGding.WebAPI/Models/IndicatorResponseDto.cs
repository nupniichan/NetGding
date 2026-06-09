using NetGding.Contracts.Models.Analysis;

namespace NetGding.WebApi.Models;

public sealed record IndicatorResponseDto(
    IndicatorSummaryDto Summary,
    IndicatorSnapshot? Detail);
