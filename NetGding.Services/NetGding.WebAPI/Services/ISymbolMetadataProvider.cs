using NetGding.WebApi.Models;

namespace NetGding.WebApi.Services;

public interface ISymbolMetadataProvider
{
    IReadOnlyList<SupportedSymbol> GetSupportedSymbols();
    IReadOnlyList<string> GetSupportedTimeframes();
}
