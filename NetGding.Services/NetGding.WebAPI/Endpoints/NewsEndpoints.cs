using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetGding.Configurations.Options;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Endpoints;

public static class NewsEndpoints
{
    public static void MapNewsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/news/{symbol}", HandleGetNewsAsync)
           .WithName("GetNewsBySymbol")
           .WithTags("News");
    }

    private static async Task<IResult> HandleGetNewsAsync(
        [FromRoute] string symbol,
        [FromQuery] int limit,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        INewsProvider newsProvider,
        IOptions<WebApiOptions> webApiOptions,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return Results.BadRequest("Symbol is required.");

        var o = webApiOptions.Value;
        var normalizedLimit = limit <= 0 ? o.NewsDefaultLimit : Math.Min(limit, o.NewsMaxLimit);
        var normalizedSymbol = symbol.Trim();

        var items = await newsProvider
            .GetNewsAsync(normalizedSymbol, normalizedLimit, from, to, ct)
            .ConfigureAwait(false);

        return Results.Ok(new
        {
            Symbol = normalizedSymbol,
            Count = items.Count,
            Items = items
        });
    }
}
