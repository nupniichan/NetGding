using System;

namespace NetGding.WebApi.Models;

public sealed record FearAndGreedDto(
    int Value,
    string Classification,
    DateTime TimestampUtc);
