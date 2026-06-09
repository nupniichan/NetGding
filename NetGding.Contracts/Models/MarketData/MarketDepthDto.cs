using System.Collections.Generic;

namespace NetGding.Contracts.Models.MarketData;

public sealed record MarketDepthDto(
    string Symbol,
    string Exchange,
    IReadOnlyList<DepthEntryDto> Bids,
    IReadOnlyList<DepthEntryDto> Asks,
    double Spread,
    double SpreadPercentage);

public readonly record struct DepthEntryDto(double Price, double Quantity);
