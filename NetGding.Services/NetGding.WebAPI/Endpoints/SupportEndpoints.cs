using NetGding.WebApi.Models;
using NetGding.WebApi.Services;

namespace NetGding.WebApi.Endpoints;

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/support/symbols", HandleGetSymbols)
           .WithName("GetSupportedSymbols")
           .WithTags("Support");

        app.MapGet("/api/support/timeframes", HandleGetTimeframes)
           .WithName("GetSupportedTimeframes")
           .WithTags("Support");

        app.MapGet("/api/support/config", HandleGetConfig)
           .WithName("GetSupportConfig")
           .WithTags("Support");
    }

    private static IResult HandleGetSymbols(ISymbolMetadataProvider provider)
    {
        var symbols = provider.GetSupportedSymbols();
        return Results.Ok(symbols);
    }

    private static IResult HandleGetTimeframes(ISymbolMetadataProvider provider)
    {
        var timeframes = provider.GetSupportedTimeframes();
        return Results.Ok(timeframes);
    }

    private static IResult HandleGetConfig(ISymbolMetadataProvider provider, IWebHostEnvironment environment)
    {
        var version = typeof(SupportEndpoints).Assembly.GetName().Version?.ToString() ?? "unknown";
        var response = new SupportConfigResponse(
            provider.GetSupportedSymbols(),
            provider.GetSupportedTimeframes(),
            environment.EnvironmentName,
            version);

        return Results.Ok(response);
    }
}
