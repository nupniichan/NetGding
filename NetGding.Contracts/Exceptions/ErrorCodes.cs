namespace NetGding.Contracts.Exceptions;

public static class ErrorCodes
{
    // Category: System & Infrastructure (ERR_SYS_*)
    public const string InternalError = "ERR_SYS_INTERNAL";
    public const string HttpRequestFailed = "ERR_SYS_HTTP_REQUEST_FAILED";
    public const string ProxyFailed = "ERR_SYS_PROXY_FAILED";
    public const string Unknown = "ERR_SYS_UNKNOWN";

    // Category: Gateway & Routing (ERR_GATEWAY_*)
    public const string CollectorGatewayFailed = "ERR_GATEWAY_COLLECTOR_FAILED";
    public const string CollectorGatewayDepthFailed = "ERR_GATEWAY_COLLECTOR_DEPTH_FAILED";
    public const string DomEmpty = "ERR_GATEWAY_DOM_EMPTY";

    // Category: Market Data & Data Collector (ERR_MARKET_*)
    public const string CollectorNotFound = "ERR_MARKET_COLLECTOR_NOT_FOUND";
    public const string NoMarketData = "ERR_MARKET_NO_DATA";
    public const string MarketDataFetchFailed = "ERR_MARKET_DATA_FETCH_FAILED";
    public const string ChartRenderFailed = "ERR_MARKET_CHART_RENDER_FAILED";
    public const string FearAndGreedFetchFailed = "ERR_MARKET_FNG_FETCH_FAILED";
    public const string NewsFetchFailed = "ERR_MARKET_NEWS_FETCH_FAILED";

    // Category: External API Providers (ERR_EXT_*)
    public const string CmcApiKeyMissing = "ERR_EXT_CMC_API_KEY_MISSING";
    public const string CmcApiEmptyResponse = "ERR_EXT_CMC_API_EMPTY_RESPONSE";
    public const string CmcApiFailed = "ERR_EXT_CMC_API_FAILED";
    public const string AlphaVantageApiError = "ERR_EXT_ALPHA_API_ERROR";
    public const string AlphaVantageRateLimit = "ERR_EXT_ALPHA_RATE_LIMIT";
    public const string AlphaVantageApiFailed = "ERR_EXT_ALPHA_API_FAILED";

    // Category: AI & LLM Analytics (ERR_AI_*)
    public const string LlmRequestFailed = "ERR_AI_LLM_REQUEST_FAILED";
    public const string LlmResponseInvalid = "ERR_AI_LLM_RESPONSE_INVALID";
}
