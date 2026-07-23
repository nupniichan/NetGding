# 📖 NetGding - System Error Codes Catalog

This document defines the standardized error code taxonomy and error handling specification implemented across all microservices and bot clients in the **NetGding** solution (`WebAPI`, `Collector`, `Analyzer`, `Telegram Bot`, `Discord Bot`).

---

## 1. Error Handling Architecture

All services in the **NetGding** ecosystem communicate errors using a unified, dual-layer exception model:

1. **`NetGdingException`**: A custom C# exception encapsulating three core properties:
   - `ErrorCode`: A standardized string identifier defined in `ErrorCodes.cs`.
   - `Location`: The origin method context (`Class.MethodName`).
   - `Message`: Human-readable error details explaining the failure.
2. **`ErrorResponse`**: The standard JSON payload returned to API consumers and bot gateways:
   ```json
   {
     "errorCode": "ERR_EXT_CMC_API_KEY_MISSING",
     "location": "CoinMarketCapFearAndGreedProvider.GetLatestAsync",
     "message": "CoinMarketCap API Key is not configured in settings."
   }
   ```

---

## 2. Naming Conventions & Categories

Error codes follow a strict prefix format: `ERR_<CATEGORY>_<DESCRIPTOR>`

| Prefix | Category | Scope & Purpose |
| :--- | :--- | :--- |
| `ERR_SYS_*` | **System & Infrastructure** | Unhandled internal errors, generic HTTP failures, proxy gateway faults. |
| `ERR_GATEWAY_*` | **Gateway & Routing** | Inter-service communications between `WebAPI` and `Collector`. |
| `ERR_MARKET_*` | **Market Data & Collector** | Market data fetching (OHLCV, Order Book depth, Charts, News, Sentiment). |
| `ERR_EXT_*` | **External API Providers** | Third-party data provider failures (CoinMarketCap, AlphaVantage). |
| `ERR_AI_*` | **AI & LLM Analytics** | Strategy generation failures in the LLM engine. |

---

## 3. Detailed Error Code Reference

### 3.1. System & Infrastructure (`ERR_SYS_*`)

| Error Code | Constant (`ErrorCodes`) | Origin Location | Description & Root Cause | Troubleshooting & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| `ERR_SYS_INTERNAL` | `ErrorCodes.InternalError` | All Endpoints / Services | Unhandled exception occurred during processing. | Inspect service logs (`Serilog`/Console) for stack trace details. |
| `ERR_SYS_HTTP_REQUEST_FAILED` | `ErrorCodes.HttpRequestFailed` | Collector / WebAPI | Internal inter-service HTTP request failed. | Verify local network connectivity and service URL settings. |
| `ERR_SYS_PROXY_FAILED` | `ErrorCodes.ProxyFailed` | WebAPI `AnalysisEndpoints` | WebAPI received a null response from Collector Service. | Ensure Collector Service is running (`docker ps` / HTTP healthcheck). |
| `ERR_SYS_UNKNOWN` | `ErrorCodes.Unknown` | Telegram / Discord Bot | Fallback error when bot catches an unhandled `Exception`. | Review bot console logs for raw inner exception details. |

---

### 3.2. Gateway & Routing (`ERR_GATEWAY_*`)

| Error Code | Constant (`ErrorCodes`) | Origin Location | Description & Root Cause | Troubleshooting & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| `ERR_GATEWAY_COLLECTOR_FAILED` | `ErrorCodes.CollectorGatewayFailed` | `CollectorGateway.AnalyzeOnDemandAsync` | WebAPI failed to proxy on-demand analysis to Collector. | Verify `CollectorServiceUrl` setting in `appsettings.json`. |
| `ERR_GATEWAY_COLLECTOR_DEPTH_FAILED` | `ErrorCodes.CollectorGatewayDepthFailed` | `CollectorGateway.GetDepthAsync` | WebAPI failed to fetch Order Book depth from Collector. | Check Collector Gateway logs and target exchange API status. |
| `ERR_GATEWAY_DOM_EMPTY` | `ErrorCodes.DomEmpty` | WebAPI `AnalysisEndpoints` | Order book depth (DOM) returned no data for target pair. | Confirm symbol and exchange support DOM/Order Book depth queries. |

---

### 3.3. Market Data & Data Collector (`ERR_MARKET_*`)

| Error Code | Constant (`ErrorCodes`) | Origin Location | Description & Root Cause | Troubleshooting & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| `ERR_MARKET_COLLECTOR_NOT_FOUND` | `ErrorCodes.CollectorNotFound` | `OnDemandAnalyzer.FetchMarketBarsAsync` | No collector resolved for requested `Exchange` & `MarketType`. | Check if exchange (`binance`, `okx`) or market type (`spot`, `future`) is valid. |
| `ERR_MARKET_NO_DATA` | `ErrorCodes.NoMarketData` | `OnDemandAnalyzer.FetchMarketBarsAsync` | Target exchange returned empty OHLCV bars. | Verify symbol format (e.g. `BTCUSDT`), timeframe, or query window. |
| `ERR_MARKET_DATA_FETCH_FAILED` | `ErrorCodes.MarketDataFetchFailed` | `BinanceMarketDataCollector`, `OkxMarketDataCollector` | Direct API request to Binance or OKX endpoint failed. | Check IP rate limiting, network status, or exchange API maintenance notices. |
| `ERR_MARKET_CHART_RENDER_FAILED` | `ErrorCodes.ChartRenderFailed` | `OnDemandAnalyzer.RenderChartIfEnabledAsync` | Technical chart rendering failed or returned empty image bytes. | Inspect SkiaSharp / ChartRenderer service status and input OHLCV bars. |
| `ERR_MARKET_FNG_FETCH_FAILED` | `ErrorCodes.FearAndGreedFetchFailed` | `OnDemandAnalyzer.FetchFearAndGreedFromWebApiAsync` | Collector failed to fetch Fear & Greed Index from WebAPI. | Check `WebApiBaseUrl` configuration in `CollectorOptions`. |
| `ERR_MARKET_NEWS_FETCH_FAILED` | `ErrorCodes.NewsFetchFailed` | `OnDemandAnalyzer.FetchNewsFromWebApiAsync` | Collector failed to fetch market news articles from WebAPI. | Check `WebApiBaseUrl` configuration and news provider API key validity. |

---

### 3.4. External API Providers (`ERR_EXT_*`)

| Error Code | Constant (`ErrorCodes`) | Origin Location | Description & Root Cause | Troubleshooting & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| `ERR_EXT_CMC_API_KEY_MISSING` | `ErrorCodes.CmcApiKeyMissing` | `CoinMarketCapFearAndGreedProvider` | CoinMarketCap API Key is not configured in settings. | Set `CoinMarketCapApiKey` in `appsettings.json` or environment variables. |
| `ERR_EXT_CMC_API_EMPTY_RESPONSE` | `ErrorCodes.CmcApiEmptyResponse` | `CoinMarketCapFearAndGreedProvider` | CoinMarketCap API returned an empty or invalid JSON response. | Verify CoinMarketCap API endpoint status and credit quota. |
| `ERR_EXT_CMC_API_FAILED` | `ErrorCodes.CmcApiFailed` | `CoinMarketCapFearAndGreedProvider` | HTTP request to CoinMarketCap endpoint failed. | Check outbound internet connection, DNS, or API key validity. |
| `ERR_EXT_ALPHA_API_ERROR` | `ErrorCodes.AlphaVantageApiError` | `AlphaVantageNewsProvider` | AlphaVantage API returned an explicit error response body. | Inspect target ticker symbol or error message returned by AlphaVantage. |
| `ERR_EXT_ALPHA_RATE_LIMIT` | `ErrorCodes.AlphaVantageRateLimit` | `AlphaVantageNewsProvider` | AlphaVantage free tier API rate limit was exceeded. | Wait for the cooldown period or upgrade AlphaVantage API tier. |
| `ERR_EXT_ALPHA_API_FAILED` | `ErrorCodes.AlphaVantageApiFailed` | `AlphaVantageNewsProvider` | AlphaVantage HTTP API call failed. | Verify outbound connectivity or AlphaVantage service availability. |

---

### 3.5. AI & LLM Analytics (`ERR_AI_*`)

| Error Code | Constant (`ErrorCodes`) | Origin Location | Description & Root Cause | Troubleshooting & Resolution |
| :--- | :--- | :--- | :--- | :--- |
| `ERR_AI_LLM_REQUEST_FAILED` | `ErrorCodes.LlmRequestFailed` | `LlmAnalyzer.CallChatCompletionAsync` | OpenAI / DeepSeek / Custom LLM API request failed. | Verify API Key, Model Name, Base URL, and account billing balance. |
| `ERR_AI_LLM_RESPONSE_INVALID` | `ErrorCodes.LlmResponseInvalid` | `LlmAnalyzer.CallChatCompletionAsync` | LLM returned an invalid response structure or error payload. | Check LLM provider/model status, request token count, and verify the model output format. |

---

## 💻 4. Developer Integration Guide

### 4.1. Throwing Exceptions with Standardized Error Codes

```csharp
using NetGding.Contracts.Exceptions;

// Throw exception with strongly-typed error code and location context
throw new NetGdingException(
    ErrorCodes.CmcApiKeyMissing,
    "CoinMarketCapFearAndGreedProvider.GetLatestAsync",
    "CoinMarketCap API Key is not configured in settings.");
```

### 4.2. Returning `ErrorResponse` in Minimal API Endpoints

```csharp
using NetGding.Contracts.Exceptions;
using NetGding.Contracts.Models.Analysis;

catch (NetGdingException ex)
{
    logger.LogError(ex, "Analysis failed [{ErrorCode} at {Location}]: {Message}",
        ex.ErrorCode, ex.Location, ex.Message);
        
    var errorResponse = new ErrorResponse(ex.ErrorCode, ex.Location, ex.Message);
    return Results.Json(errorResponse, statusCode: 500);
}
```

### 4.3. Formatting Error Responses in Chat Bots (Telegram / Discord)

When an API call returns an error, bot clients extract the `ErrorCode` to render clean Markdown embeds:

>  **Analysis Failed**
> - **Code:** `ERR_MARKET_NO_DATA`
> - **Location:** `OnDemandAnalyzer.FetchMarketBarsAsync`
> - **Message:** No market data (OHLCV) found for BTCUSDT on binance [1h].
