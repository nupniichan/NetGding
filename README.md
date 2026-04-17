<p align="center">
  <h1 align="center">NetGding</h1>
  <p align="center">
    <b>Just an AI-powered market analysis</b>
    <br/>
    <i>Collect market data · Analyze with LLM & technical indicators</i>
  </p>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker" alt="Docker" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License" />
</p>

---

## What is NetGding?

NetGding is a **microservice-based trading analysis system** built with .NET 10. It automates the entire pipeline from raw market data collection to AI-driven signal generation and instant delivery through chat bots.

**It does NOT execute trades.** Instead, it acts as an intelligent analysis assistant collecting OHLCV data and news from Alpaca Markets, computing technical indicators (EMA, MACD, RSI, Bollinger Bands, ATR, VWAP, Support/Resistance), feeding everything into an LLM for signal analysis, applying a rule-based Signal Engine for guardrails, and rendering TradingView-style charts — all delivered to your Telegram or Discord in seconds.

---

## Important note

This bot is currently under active development and continuous updates. Some features, signals, or outputs may not function as expected or could contain inaccuracies. Users should be aware that the system is still being improved, and results may change over time as adjustments are made.

## Services

| Service | Port | Description |
|---------|------|-------------|
| **Collector** | `8081` | Core engine, collects OHLCV bars & news from Alpaca, runs scheduled & on-demand analysis |
| **WebAPI** | `8080` | Central REST gateway, stores results, forwards notifications, serves Swagger UI |
| **Telegram** | `8080` | Telegram bot, long-poll for commands, sends formatted analysis with charts |
| **Discord** | `8080` | Discord bot, slash commands, sends rich embed analysis with charts |

### Shared Libraries

| Library | Description |
|---------|-------------|
| **NetGding.Analyzer** | Technical indicator calculations, LLM prompt builder & parser, Signal Engine, Risk Calculator, FinBERT sentiment |
| **NetGding.ChartRenderer** | Generates TradingView-style candlestick + volume charts (ScottPlot + SkiaSharp) |
| **NetGding.Contracts** | Shared models — `AnalysisResult`, `OhlcvBar`, `NewsArticle`, `IndicatorSnapshot`, enums |
| **NetGding.Configurations** | Options classes, `.env` file loader, HTTP retry helper |

---

## How It Works

### 1. Data Collection
Three background workers run inside the **Collector** service:

- **CollectorWorker** — Polls Alpaca at each bar boundary (D1, W1, M1) for OHLCV data per configured symbol. Saves to JSON.
- **NewsCollectorWorker** — Polls Alpaca News at configurable intervals. Saves articles per symbol to JSON.
- **AnalysisWorker** — Waits for each bar boundary, then runs full analysis for every symbol and publishes results to WebAPI.

### 2. Analysis Pipeline
When analysis is triggered (scheduled or on-demand via `/analyze`):

1. **Fetch** — OHLCV bars and recent news from Alpaca
2. **Compute Indicators** — EMA (9/21/50/100/200), MACD, RSI, Bollinger Bands, ATR, Volume MA, VWAP, Support/Resistance levels (auto-selected based on timeframe group: Intraday / Swing / Position)
3. **Detect Market Regime** — Trending, Ranging, or Volatile
4. **LLM Analysis** — Build structured prompt with bars + indicators + news → send to LLM (OpenRouter/Gemma) → receive JSON signal (trend, momentum, volatility, confidence, newsImpact)
5. **Signal Engine** — Apply guardrails: minimum confidence threshold, EMA alignment check, reversal stability filter
6. **Risk Calculator** — Generate Entry/Stop-Loss/Take-Profit (Futures) or Buy price (Spot)
7. **Chart Rendering** — Generate candlestick chart with overlays (EMA, BB, VWAP, S/R, risk lines, decision marker)
8. **Publish** — Send `AnalysisNotification` (result + Base64 chart) to WebAPI → forward to Telegram & Discord

### 3. Delivery
- **WebAPI** stores results in-memory & forwards to both chat bots
- **Telegram Bot** sends Markdown messages with chart images
- **Discord Bot** sends rich embeds with attached chart images

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/) *(for containerized deployment)*
- [Alpaca Markets account](https://alpaca.markets/) *(free paper trading account works)*
- LLM API key *(optional — works with free-tier OpenRouter models or you can use your local API AI like gamma4 or others site)*

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
| `Alpaca_ApiKey` | Alpaca API key |
| `Alpaca_ApiSecret` | Alpaca API secret |
| `Llm_ApiKey` | LLM provider API key (OpenRouter, etc.) |
| `Telegram_BotToken` | Telegram bot token from [@BotFather](https://t.me/BotFather) |
| `Telegram_ChatId` | Target Telegram chat ID |
| `Discord_BotToken` | Discord bot token |
| `Discord_ChannelId` | Discord channel ID for notifications |
| `Discord_GuildId` | Discord server (guild) ID |

### 2a. Run with Docker Compose (Recommended)

```bash
docker compose up --build -d
```

Services will be available at:

| Service | URL |
|---------|-----|
| Collector | `http://localhost:5000` |
| WebAPI | `http://localhost:5001` |
| Telegram | `http://localhost:5002` |
| Discord | `http://localhost:5003` |
| Swagger UI | `http://localhost:5001/swagger` *(dev only)* |

### 2b. Run Locally (Development)

```bash
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
| `/start` or `/help` | Show available commands and indicator legend |
| `/latest <symbol>` | Get the most recent cached analysis (D1+) |
| `/analyze <symbol> <timeframe>` | Trigger a live on-demand analysis |

### Discord Slash Commands

| Command | Description |
|---------|-------------|
| `/help` | Show available commands and indicator legend |
| `/latest <symbol>` | Get the most recent cached analysis (D1+) |
| `/analyze <symbol> <timeframe>` | Trigger a live on-demand analysis |

### Supported Timeframes

`15m` · `1h` · `4h` · `1d` · `1w` · `1m` ( Will add more in the future )

### Examples

```
/analyze BTC 4h        → Analyze BTC/USD on 4-hour timeframe
/analyze ETH/USD 1d    → Analyze ETH/USD on daily timeframe
/latest SOL            → Get latest cached analysis for SOL/USD
```

### REST API

When WebAPI is running, the following endpoints are available:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/analysis/on-demand` | Trigger on-demand analysis `{symbol, timeframe}` |
| `POST` | `/api/analysis/publish` | Publish result & forward to bots |
| `GET` | `/api/analysis/latest/{symbol}?timeframe=1d` | Get latest result |
| `GET` | `/api/analysis/history/{symbol}?timeframe=1d&page=1&pageSize=20` | Paginated history |
| `GET` | `/api/news/{symbol}` | Get collected news articles |
| `GET` | `/api/indicators/{symbol}?timeframe=1d` | Get computed indicators |
| `GET` | `/api/health` | Health check for all services |

Full interactive documentation available at `/swagger` in development mode.

---

## Configuration Reference

### Collector Settings (`appsettings.json` → `Collector` section)

| Key | Default | Description |
|-----|---------|-------------|
| `UsePaper` | `true` | Use Alpaca paper trading environment |
| `Symbols` | `["BTC/USD", "ETH/USD", "SOL/USD", "PAXG/USD"]` | Symbols to track |
| `BarTimeFrames` | `["15m", "1h", "4h", "1d", "1w", "1m"]` | Timeframes for data collection |
| `LookbackDays` | `30` | Minimum OHLCV lookback |
| `NewsEnabled` | `true` | Enable news collection |
| `NewsPollingIntervalMinutes` | `5` | News polling frequency |
| `AnalysisEnabled` | `true` | Enable scheduled analysis |
| `ChartEnabled` | `true` | Attach chart images to results |
| `WebApiPublishEnabled` | `false` | Auto-publish analysis to WebAPI |

### LLM Settings (`Llm` section)

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `https://openrouter.ai/api/v1` | LLM API base URL ( or if you use local model then change it to your api url ) |
| `ModelName` | `google/gemma-4-26b-a4b-it:free` | Model to use |
| `Temperature` | `0.3` | LLM temperature |
| `MaxTokens` | `2048` | Max response tokens |
| `MaxAttempts` | `3` | Retry attempts on rate limit |

---

## Project Structure

```
NetGding/
├── NetGding.Configurations/        # Shared config — options, env loader, retry helper
│   ├── Options/                    # CollectorOptions, TelegramOptions, DiscordOptions, WebApiOptions
│   └── Bootstrap/                  # EnvFileLoader, HttpRetryHelper
├── NetGding.Contracts/             # Shared models & interfaces
│   └── Models/
│       ├── Analysis/               # AnalysisResult, LlmSignal, IndicatorSnapshot, RiskManagement
│       ├── MarketData/             # OhlcvBar, OhlcvSeries
│       ├── News/                   # NewsArticle, NewsCollection
│       └── Indicators/             # EMA, MACD, RSI, BollingerBands, ATR, Volume, VWAP
├── NetGding.Services/
│   ├── NetGding.Analyzer/          # Analysis logic library
│   │   ├── Indicators/             # TrendCalculator, MomentumCalculator, VolatilityCalculator, etc.
│   │   ├── Llm/                    # LlmAnalyzer — prompt builder, API caller, response parser
│   │   ├── Signal/                 # SignalEngine — guardrails, EMA filter, reversal suppression
│   │   ├── FinBert/                # FinBERT sentiment analysis via HuggingFace Inference API
│   │   └── Gemma/                  # Gemma model integration
│   ├── NetGding.ChartRenderer/     # TradingView-style chart generation (ScottPlot + SkiaSharp)
│   ├── NetGding.Collector/         # Data collection & analysis orchestration service
│   │   ├── Workers/                # CollectorWorker, NewsCollectorWorker, AnalysisWorker
│   │   ├── Alpaca/                 # AlpacaOhlcvCollector, AlpacaNewsCollector
│   │   ├── Services/               # OnDemandAnalyzer, WebApiAnalysisPublisher
│   │   └── Persistence/            # JSON file persistence
│   ├── NetGding.WebAPI/            # Central REST API gateway
│   │   ├── Endpoints/              # Analysis, News, Indicators, Health, Support endpoints
│   │   └── Services/               # Store, Forwarders, CollectorGateway
│   ├── NetGding.Telegram/          # Telegram bot service
│   │   ├── Services/               # BotPollingService, TelegramNotifier
│   │   └── Formatting/             # AnalysisMessageFormatter
│   └── NetGding.Discord/           # Discord bot service
│       ├── Commands/               # Slash commands (AnalysisCommands)
│       ├── Services/               # DiscordBotService, DiscordNotifier
│       └── Formatting/             # AnalysisEmbedFormatter
├── docker-compose.yml
├── .env.example
└── NetGding.sln
```

---

## Developer Guide

### Adding a New Indicator

1. Create the indicator model in `NetGding.Contracts/Models/Indicators/`
2. Implement the calculation in `NetGding.Analyzer/Indicators/`
3. Add values to `IndicatorSnapshot` in `NetGding.Contracts/Models/Analysis/`
4. Call your calculator from `OnDemandAnalyzer.ComputeIndicators()`

### Adding a New Symbol Type

Symbols containing `/` are treated as **Crypto** (e.g., `BTC/USD`), others as **Stock**. This is resolved in `OnDemandAnalyzer.ResolveMarket()`.

### Extending the Signal Engine

The `SignalEngine` applies three filters in order:
1. **Confidence threshold** — reject signals below `MinConfidence`
2. **EMA guardrail** — reject BUY if fast EMA < slow EMA (and vice versa)
3. **Reversal stability** — require higher confidence for direction reversals

Add new filters by modifying `SignalEngine.Evaluate()`.

### Environment Variables

All settings support override via environment variables following the `Section_Key` naming convention (double underscore `__` for nested keys in Docker). See `.env.example` for the full list.

## Disclaimer

All trading signals, insights, and information provided by this bot are for reference purposes only and do not constitute financial advice. The developers are not responsible for any financial losses or decisions made based on the bot’s output. Always conduct your own research and consider consulting with a qualified financial advisor before making any trading decisions.

Thanks for visiting my repository <3
