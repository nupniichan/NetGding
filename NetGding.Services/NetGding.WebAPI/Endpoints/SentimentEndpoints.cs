using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetGding.WebApi.Services;
using System.Threading;
using System.Threading.Tasks;

namespace NetGding.WebApi.Endpoints;

public static class SentimentEndpoints
{
    public static void MapSentimentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fear-and-greed", HandleGetFearAndGreedAsync)
           .WithName("GetFearAndGreedIndex")
           .WithTags("Sentiment");
    }

    private static async Task<IResult> HandleGetFearAndGreedAsync(
        IFearAndGreedProvider provider,
        CancellationToken ct)
    {
        var fng = await provider.GetLatestAsync(ct).ConfigureAwait(false);
        if (fng is null)
            return Results.StatusCode(502);

        return Results.Ok(fng);
    }
}
