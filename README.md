<p align="center">
  <h1 align="center">NetGding</h1>
  <p align="center">
    <b>A simple market analysis using AI</b>
    <br/>
    <i>Collect market data · Analyze with LLM & technical indicators</i>
  </p>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker" alt="Docker" />
  <img src="https://img.shields.io/badge/license-Apache%20License%202.0-blue" alt="License" />
</p>

---

## What is NetGding?

NetGding is an event-driven microservice-based trading analysis system built with .NET 10. It automates the entire pipeline from raw market data collection to AI-driven signal generation and instant delivery through chat bots using asynchronous Redis Pub/Sub messaging.

It **does NOT execute trades**. Instead, it acts as an intelligent analysis assistant collecting OHLCV data from Binance and OKX, computing technical indicators (EMA, MACD, RSI, Bollinger Bands, ATR, VWAP, Support/Resistance), fetching market news and global sentiment, feeding everything into a LLM for signal analysis, applying a rule-based Signal Engine for guardrails, and rendering advanced charts. All results are persisted and broadcast asynchronously to Telegram and Discord in seconds.

---

## System Architecture
<img width="1857" height="835" alt="Diagram" src="https://github.com/user-attachments/assets/2735c072-5ee0-4e28-87be-fb96a079f350" />

---

## Important note

This bot is currently under active development and continuous updates. Some features, signals, or outputs may not function as expected or could contain inaccuracies. Users should be aware that the system is still being improved and results may change over time as adjustments are made.

---

## Services

| Service | Port | Description |
|---------|------|-------------|
| **Redis** | `6379` | Central Redis message broker (Pub/Sub EventBus) and multi-tier analysis and market data cache. |
| **Collector** | `5000` | Core engine, fetches OHLCV bars (cached via `CachedMarketDataProvider`), computes indicators, runs LLM analysis, applies Signal Engine guardrails, renders charts, and publishes `analysis:completed` events. |
| **WebAPI** | `5001` | Central REST gateway, caches news in SQLite (`SqliteNewsCacheStore`), persists analysis results in SQLite (`trading.db`), handles composite news and sentiment APIs, and routes requests/responses via Redis EventBus. |
| **Telegram** | `5002` | Telegram bot service, uses long-polling for commands and listens to `analysis:completed` Redis events to publish analysis with charts. |
| **Discord** | `5003` | Discord bot service, registers slash commands and listens to `analysis:completed` Redis events to post rich embeds with charts. |

### Shared Libraries

| Library | Description |
|---------|-------------|
| **NetGding.Analyzer** | Computes technical indicators (EMA, MACD, RSI, Bollinger Bands, ATR, VWAP, Support/Resistance), resolves market regime, structures LLM prompts/parsers, runs the Signal Engine guardrails, and computes Spot risk management parameters. |
| **NetGding.ChartRenderer** | Generates TradingView charts using Chart-Img API, overlaying support and resistance lines. |
| **NetGding.Contracts** | Shared DTOs (`AnalysisResult`, `OhlcvBar`, `IndicatorSnapshot`, `NewsItem`), Event models (`AnalysisCompletedEvent`, `AnalysisRequestEvent`, `AnalysisResponseEvent`), Redis EventBus implementation (`RedisEventBus`), caching interfaces (`IAnalysisCache`, `RedisAnalysisCache`), `WebApiClient`, `JsonDefaults`, `ValidationConstants`, and `ErrorCodes`. |
| **NetGding.Configurations** | Shared configuration helpers (`RedisOptions`, `CollectorOptions`, `WebApiOptions`, etc.), `.env` file loader, HTTP retry handler (`HttpRetryHelper`), and messaging extension setup. |

---

## How It Works?

### 1. Data Collection & Caching
The collector service ([OnDemandAnalyzer.cs](NetGding.Services/NetGding.Collector/Services/OnDemandAnalyzer.cs)) checks the exchange. It uses [MarketDataCollectorResolver.cs](NetGding.Services/NetGding.Collector/Services/MarketData/MarketDataCollectorResolver.cs) to select either [BinanceMarketDataCollector.cs](NetGding.Services/NetGding.Collector/Services/MarketData/BinanceMarketDataCollector.cs) or [OkxMarketDataCollector.cs](NetGding.Services/NetGding.Collector/Services/MarketData/OkxMarketDataCollector.cs). It uses [CachedMarketDataProvider.cs](NetGding.Services/NetGding.Collector/Services/MarketData/CachedMarketDataProvider.cs) to cache candles in Redis/Memory, preventing exchange rate limits. Symbols are flexibly normalized (for example, `BTC`, `BTC/USDT`, or `BTCUSDT`).

### 2. Technical Indicators
The system computes various indicators using [NetGding.Analyzer](NetGding.Services/NetGding.Analyzer):
* **EMA**: Exponential Moving Averages. The system filters periods based on timeframe groups:
  * *Intraday (15m, 1h, 4h)*: EMA 9, 21, 50.
  * *Swing (1d)*: EMA 9, 21, 50, 100, 200.
  * *Position (1w, 1m)*: EMA 21, 50, 100, 200.
* **MACD**: Moving Average Convergence Divergence (Line, Signal, Histogram).
* **RSI**: Relative Strength Index (Period 14).
* **Bollinger Bands**: Period 20, Multiplier 2.0.
* **ATR**: Average True Range (Wilder's ATR 14).
* **Volume MA**: Average Volume over the matching EMA periods.
* **VWAP**: Volume Weighted Average Price (Only computed for *Intraday* group, resets every UTC day).
* **Support & Resistance**: Finds swing points using a wing size (5 for intraday, 10 for swing/position). It groups them using clustering (threshold is `ATR / 2.0`) to generate up to 3 Support levels (S1, S2, S3) and 3 Resistance levels (R1, R2, R3) ([SupportResistanceCalculator.cs](NetGding.Services/NetGding.Analyzer/Indicators/SupportResistanceCalculator.cs)).

### 3. Market Regime Detection
The system automatically classifies the current market regime using [MarketRegimeDetector.cs](NetGding.Services/NetGding.Analyzer/Signal/MarketRegimeDetector.cs):
* **Volatile**: True if the ATR divided by the current price is greater than 2% (`ATR / Price > 0.02`).
* **Trending**: True if fast EMA (9) and slow EMA (21) spread is greater than 0.5% AND the MACD histogram has directional momentum (absolute histogram value is greater than 0).
* **Ranging**: The default state if the market is not volatile and not trending.

### 4. LLM Analysis & Composite News Integration
It builds a structural text prompt containing the OHLCV bars, calculated indicator values, market news articles, and sentiment data (like the CoinMarketCap Fear & Greed Index). News items are aggregated from multiple providers ([GoogleNewsRssNewsProvider.cs](NetGding.Services/NetGding.WebAPI/Services/GoogleNewsRssNewsProvider.cs), [AlphaVantageNewsProvider.cs](NetGding.Services/NetGding.WebAPI/Services/AlphaVantageNewsProvider.cs)) and cached in SQLite with a 6-hour refresh interval and 5-day retention ([SqliteNewsCacheStore.cs](NetGding.Services/NetGding.WebAPI/Services/SqliteNewsCacheStore.cs)). The prompt is sent to the LLM (for example, OpenRouter or local Gemma) to get a JSON response with:
* `trend`: bullish, bearish, or neutral.
* `momentum`: strong, weak, or divergence.
* `volatility`: high or low.
* `confidence`: 0.0 to 1.0 (requires high confluence of indicators for scores above 0.90).
* `reason`: 1-2 sentence market analysis.
* `newsImpact`: -1.0 to 1.0.

### 5. Signal Engine Guardrails
The raw LLM signal goes through [SignalEngine.cs](NetGding.Services/NetGding.Analyzer/Signal/SignalEngine.cs):
* **Confidence Check**: Rejects signals below the minimum confidence threshold.
* **EMA Alignment**: Rejects BUY if EMA 9 is below EMA 21 (or SELL if EMA 9 is above EMA 21) when in a trending market.
* **Stability Filter**: Suppresses rapid trade reversals (Buy to Sell or vice versa) unless the new signal exceeds a higher confidence threshold (`ReversalConfidence`).

### 6. Spot Risk Calculator (DYOR)
If a trade signal is valid, [RiskCalculator.cs](NetGding.Services/NetGding.Analyzer/Signal/RiskCalculator.cs) computes Spot trade parameters:
* **Buy Price**: Current market entry price.
* **Dollar-Cost Averaging**: Computes `DCA1 = Price - ATR` and `DCA2 = Price - ATR * 2`.
* **Stop Loss & Take Profit**: Computes risk levels based on ATR multipliers.

### 7. Chart Rendering
Uses the external Chart-Img API service ([AnalysisChartRenderer.cs](NetGding.Services/NetGding.ChartRenderer/AnalysisChartRenderer.cs)) to render TradingView-style candlestick and volume charts. It adds horizontal line drawings for the computed Support and Resistance levels (using green for Support and red for Resistance).

### 8. Event-Driven Persistence & Delivery
When analysis completes, the Collector publishes an `analysis:completed` event to the Redis EventBus. Subscriber workers in WebAPI ([AnalysisCompletedSubscriberWorker.cs](NetGding.Services/NetGding.WebAPI/Workers/AnalysisCompletedSubscriberWorker.cs)), Telegram ([TelegramAnalysisSubscriberWorker.cs](NetGding.Services/NetGding.Telegram/Services/TelegramAnalysisSubscriberWorker.cs)), and Discord ([DiscordAnalysisSubscriberWorker.cs](NetGding.Services/NetGding.Discord/Services/DiscordAnalysisSubscriberWorker.cs)) consume the event asynchronously to save the result to SQLite and publish formatted messages with charts to their target channels.

---

## Getting Started

### Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Docker & Docker Compose](https://docs.docker.com/get-docker/) *(for containerized deployment)*
* LLM API key *(works will both local or any others providers just do not forget to change the .env file )*
* Redis 7.0+ *(included in docker-compose)*

### 1. Clone & Configure

```bash
git clone https://github.com/nupniichan/NetGding.git
cd NetGding
```

Copy the example environment file and fill in your secrets:

```bash
cp .env.example .env
```

**Required variables:**

| Variable | Description |
|----------|-------------|
| `Redis__ConnectionString` | Redis connection string (e.g. `localhost:6379` or `redis:6379`). |
| `Llm_ApiKey` | LLM provider API key (OpenRouter, etc.). |
| `Telegram_BotToken` | Telegram bot token from [@BotFather](https://t.me/BotFather). |
| `Telegram_ChatId` | Target Telegram chat ID. |
| `Discord_BotToken` | Discord bot token. |
| `Discord_ChannelId` | Discord channel ID for notifications. |
| `Discord_GuildId` | Discord server (guild) ID. |
| `CoinMarketCap_ApiKey` | API key for fetching the Crypto Fear and Greed Index. |
| `WebApi_ConnectionString` | SQLite connection string for analysis storage. |
| `AlphaVantage_ApiKey` | API key for fetching market news. |

### 2a. Run with Docker Compose (Recommended)

```bash
docker compose up --build -d
```

> [!NOTE]
> When running inside Docker, a local `./db` directory is automatically created and mounted to the WebAPI container at `/app/db` to persist the SQLite database. Ensure this directory is writable.

Services will be available at:

| Service | URL |
|---------|-----|
| Redis | `localhost:6379` |
| Collector | `http://localhost:5000` |
| WebAPI | `http://localhost:5001` |
| Telegram | `http://localhost:5002` |
| Discord | `http://localhost:5003` |
| Swagger UI | `http://localhost:5001/swagger` *(dev only)* |

### 2b. Run Locally (Development)

```bash
# Ensure Redis server is running on localhost:6379

# Terminal 1 — Collector
dotnet run --project NetGding.Services/NetGding.Collector

# Terminal 2 — WebAPI
dotnet run --project NetGding.Services/NetGding.WebAPI

# Terminal 3 — Telegram
dotnet run --project NetGding.Services/NetGding.Telegram

# Terminal 4 — Discord
dotnet run --project NetGding.Services/NetGding.Discord
```

---

## User Guide

### Telegram Commands

| Command | Description |
|---------|-------------|
| `/start` or `/help` | Show available commands and indicator legend. |
| `/latest <symbol>` | Get the most recent cached analysis (e.g., `/latest BTC`). |
| `/analyze <symbol> <timeframe> [<exchange>]` | Trigger a live on-demand analysis (defaults: `binance`). |
| `/fagi` | Get the current Crypto Fear and Greed Index. |

### Discord Slash Commands

| Command | Description |
|---------|-------------|
| `/help` | Show available commands and indicator legend. |
| `/latest <symbol>` | Get the most recent cached analysis (e.g., `/latest BTC`). |
| `/analyze <symbol> <timeframe> [<exchange>]` | Trigger a live on-demand analysis (defaults: `binance`). |
| `/fagi` | Get the current Crypto Fear and Greed Index. |

### Supported Timeframes

`15m` · `1h` · `4h` · `1d` · `1w` · `1m` ( Will add more in the future )

### Examples

```
/analyze BTC 4h                    → Analyze BTC/USDT spot on Binance
/analyze ETH 1d okx                → Analyze ETH/USDT spot on OKX
/latest SOL                         → Get latest cached analysis for SOL/USDT
```

---

## REST API Reference

The primary endpoints exposed by the services:

### WebAPI Gateway Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/analysis/on-demand` | Trigger on-demand analysis via Redis EventBus request. |
| `POST` | `/api/analysis/publish` | Save analysis results to SQLite and broadcast via Redis EventBus. |
| `GET` | `/api/analysis/latest/{symbol}?timeframe=1d` | Get the latest cached analysis. |
| `GET` | `/api/analysis/history/{symbol}?timeframe=1d&page=1&pageSize=20` | Get paginated analysis history. |
| `GET` | `/api/news/{symbol}` | Fetch stored market news articles (cached in SQLite). |
| `GET` | `/api/indicators/{symbol}?timeframe=1d&exchange=binance` | Fetch indicators. |
| `GET` | `/api/fear-and-greed` | Fetch the current Crypto Fear & Greed Index. |
| `GET` | `/api/health` | Health check probe endpoint. |

#### Input Schema: `POST /api/analysis/on-demand`
```json
{
  "symbol": "BTC/USDT",
  "timeframe": "4h",
  "exchange": "binance",
  "chartSymbol": "BINANCE:BTCUSDT",
  "chartOnly": false
}
```

### Collector Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/analysis/on-demand` | Trigger direct analysis pipeline. |
| `GET` | `/api/market/dom?symbol=BTCUSDT&exchange=binance` | Get raw Depth of Market (Order Book) data. |

Full interactive documentation is available at `/swagger` when running in development mode.

---

## Configuration Reference

### Redis Settings (`appsettings.json` / `.env`)

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionString` | `localhost:6379` | Redis server connection string. |

### WebAPI Settings (`appsettings.json` → `WebApi` section)

| Key | Default | Description |
|-----|---------|-------------|
| `CollectorServiceUrl` | `http://localhost:5000` | Endpoint URL of the Collector service. |
| `TimeoutSeconds` | `20` | Default HTTP request timeout. |
| `CollectorTimeoutSeconds` | `30` | Timeout when querying the Collector. |
| `ConnectionString` | `Data Source=db/trading.db` | SQLite connection string. |
| `NewsDefaultLimit` | `20` | Default count of news items returned. |
| `NewsCacheRefreshHours` | `6` | Refresh interval in hours for news SQLite cache. |
| `NewsCacheRetentionDays` | `5` | Retention period in days for news SQLite cache. |

### Collector Settings (`appsettings.json` → `Collector` section)

| Key | Default | Description |
|-----|---------|-------------|
| `LookbackDays` | `30` | Minimum lookup period in days for candles. |
| `WebApiPublishEnabled` | `false` | Automatically publishes computed analysis results. |
| `WebApiBaseUrl` | `http://localhost:5001` | Gateway API URL. |
| `ChartEnabled` | `true` | Enables/Disables rendering charts. |
| `ChartImgApiKey` | `""` | API key from chart-img.com to request TradingView charts. |

### LLM Settings (`appsettings.json` → `Llm` section)

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `https://openrouter.ai/api/v1` | Target LLM endpoint. |
| `ModelName` | `google/gemma-4-26b-a4b-it:free` | Model name. |
| `Temperature` | `0.3` | Controls model output variance. |
| `MaxTokens` | `2048` | Maximum token limit in response. |
| `MaxAttempts` | `3` | Attempts to retry if rate-limited. |

### Signal Engine Settings (`appsettings.json` → `SignalEngine` section)

| Key | Default | Description |
|-----|---------|-------------|
| `MinConfidence` | `0.6` | Minimum confidence required to evaluate trade direction. |
| `TradeConfidence` | `0.65` | Confidence required to proceed with standard entry signals. |
| `ReversalConfidence` | `0.8` | Confidence required to overturn an active trend direction. |
| `AtrSlMultiplier` | `1.5` | Stop-loss distance multiplier for ATR. |
| `AtrTpMultiplier` | `2.0` | Take-profit distance multiplier for ATR. |
| `FastEmaPeriod` | `"9"` | Target fast EMA identifier. |
| `SlowEmaPeriod` | `"21"` | Target slow EMA identifier. |

---

## Project Structure

```
NetGding/
├── ERROR_CODES.md                     # Centralized error codes reference
├── appsettings.Shared.json            # Shared configuration across microservices
├── redis.conf                         # Redis server configuration
├── NetGding.Configurations/           # Shared configuration & bootstrap helpers
│   ├── Bootstrap/                     # EnvFileLoader, HttpRetryHelper, MessagingServiceExtensions, HealthEndpointExtensions
│   └── Options/                       # CollectorOptions, DiscordOptions, RedisOptions, TelegramOptions, WebApiOptions
├── NetGding.Contracts/                # Shared models, DTOs, events & interfaces
│   ├── Events/                        # AnalysisCompletedEvent, AnalysisRequestEvent, AnalysisResponseEvent, EventTopics, FearAndGreedEvent, NewsDataEvent
│   ├── Exceptions/                    # ErrorCodes
│   ├── Messaging/                     # IEventBus, RedisEventBus, AnalysisSubscriberWorkerBase
│   ├── Models/
│   │   ├── Analysis/                  # AnalysisResult, LlmSignal, IndicatorSnapshot, RiskManagement, OnDemandRequest
│   │   ├── Indicators/                # EMA, MACD, RSI, BollingerBands, ATR, Volume, VWAP
│   │   ├── MarketData/                # OhlcvBar, OhlcvSeries, MarketParsingHelper
│   │   └── News/                      # NewsArticle, NewsCollection, NewsItem
│   └── Services/                      # IAnalysisCache, InMemoryAnalysisCache, RedisAnalysisCache, IWebApiClient, WebApiClient
├── NetGding.Services/
│   ├── NetGding.Analyzer/             # Analysis logic library
│   │   ├── Indicators/                # TrendCalculator, MomentumCalculator, VolatilityCalculator, SupportResistanceCalculator
│   │   ├── Llm/                       # LlmAnalyzer (prompt builder, API caller, response parser), LlmOptions
│   │   └── Signal/                    # SignalEngine, MarketRegimeDetector, RiskCalculator, SignalEngineOptions
│   ├── NetGding.ChartRenderer/        # TradingView chart generation (via Chart-Img API)
│   ├── NetGding.Collector/            # Data collection & analysis orchestration service
│   │   ├── Endpoints/                 # AnalysisEndpoints
│   │   └── Services/                  # OnDemandAnalyzer
│   │       └── MarketData/            # BinanceMarketDataCollector, OkxMarketDataCollector, MarketDataCollectorResolver, CachedMarketDataProvider
│   ├── NetGding.WebAPI/               # Central REST API gateway
│   │   ├── Endpoints/                 # AnalysisEndpoints, IndicatorEndpoints, NewsEndpoints, HealthEndpoints
│   │   ├── Persistence/               # TradingDbContext, NewsItemEntity, SqliteNewsCacheStore, INewsCacheStore
│   │   ├── Services/                  # SqliteAnalysisResultStore, CollectorHttpClient, GoogleNewsRssNewsProvider, AlphaVantageNewsProvider, CoinMarketCapFearAndGreedProvider, CompositeNewsProvider, CachedNewsProvider
│   │   └── Workers/                   # AnalysisCompletedSubscriberWorker
│   ├── NetGding.Telegram/             # Telegram bot service
│   │   ├── Formatting/                # AnalysisMessageFormatter
│   │   └── Services/                  # BotPollingService, TelegramAnalysisSubscriberWorker
│   └── NetGding.Discord/              # Discord bot service
│       ├── Commands/                  # AnalysisCommands
│       ├── Formatting/                # AnalysisEmbedFormatter
│       └── Services/                  # DiscordBotService, DiscordAnalysisSubscriberWorker
├── docker-compose.yml
├── .env.example
└── NetGding.sln
```

---

## Developer Guide

### Adding new indicator

1. Create the indicator model in [NetGding.Contracts/Models/Indicators/](NetGding.Contracts/Models/Indicators).
2. Implement the calculations inside [NetGding.Analyzer/Indicators/](NetGding.Services/NetGding.Analyzer/Indicators).
3. Append properties to [IndicatorSnapshot.cs](NetGding.Contracts/Models/Analysis/IndicatorSnapshot.cs).
4. Call your new indicator calculator method inside `OnDemandAnalyzer.ComputeIndicators()` ([OnDemandAnalyzer.cs](NetGding.Services/NetGding.Collector/Services/OnDemandAnalyzer.cs)).

### Adding new exchange or market source

1. Write a new collector class implementing `IExchangeMarketDataCollector` inside [NetGding.Collector/Services/MarketData/](NetGding.Services/NetGding.Collector/Services/MarketData).
2. Write custom logic for querying and normalizing symbol formats from the target provider.
3. Register the class inside `Program.cs` under the Collector service ([Program.cs](NetGding.Services/NetGding.Collector/Program.cs)).
4. If needed, configure the resolver mapping inside [MarketDataCollectorResolver.cs](NetGding.Services/NetGding.Collector/Services/MarketData/MarketDataCollectorResolver.cs).

### Extending the Signal Engine

The [SignalEngine.cs](NetGding.Services/NetGding.Analyzer/Signal/SignalEngine.cs) evaluates three layers of logic:
1. **Confidence Threshold**: Validates if `signal.Confidence` satisfies options requirements.
2. **EMA Guardrail**: In trending regimes, it confirms EMA stacking agrees with trade direction.
3. **Stability check**: Protects against volatile trend switching unless higher confidence limits are met.

Modify the `SignalEngine.Evaluate()` method to add new logic or rules.

---

## Disclaimer

This project is a personal, non-commercial project developed strictly for educational and research purposes; it does not provide financial advice, investment recommendations, or any form of solicitation to buy or sell financial instruments. All signals and data are for reference only and do not guarantee future results, and should not be interpreted as actionable trading instructions. The developers are not licensed financial advisors and shall not be held liable for any financial losses or decisions made based on this tool. This project is not intended for live trading or production use. **Use at your own risk**. **Always Always Always and please** conduct your own research (DYOR) and consult with a professional before trading.

---

## Acknowledgments

This project is built upon the incredible foundations provided by the open source community and professional data services. I would like to express my gratitude to the teams behind .NET, Binance and OKX public APIs, and the developers of the various libraries used for technical analysis, charting, and integration. Their incredible work enables developers like me to build complex systems with efficiency and precision. ❤️

---

## License

Take a look at [Apache License 2.0](https://github.com/nupniichan/NetGding/blob/main/LICENSE)

---

 <p align="center">Thanks for visiting my repository ⸜(｡˃ ᵕ ˂ )⸝♡</p>
