# Torra-Watch

A Windows desktop cryptocurrency trading bot that automatically monitors and trades on Binance using a momentum-reversal strategy.

## Trading Strategy

Torra-Watch implements a **dip-buying strategy** targeting oversold cryptocurrencies:

1. **Monitor Universe**: Scans the top 150 cryptocurrencies by 24-hour trading volume (USDT pairs)
2. **Identify Dips**: Calculates 3-hour returns for all coins in the universe
3. **Select Target**: Picks the **second worst performer** (not the first) that has dropped at least **-4%** in 3 hours
4. **Execute Trade**: Places a market buy order using available USDT balance
5. **Set Exits**: Automatically places an OCO (One-Cancels-Other) order with:
   - **Take Profit**: +2% above entry price
   - **Stop Loss**: -2% below entry price
   - **Time Stop**: Maximum 6-hour holding period
6. **Repeat**: After position closes (TP, SL, or time stop), waits for cooldown then starts over

### Why Second Worst Instead of First?

The strategy intentionally avoids the worst performer to filter out potential:
- Delisting announcements
- Security breaches or hacks
- Fundamental issues causing extreme drops

The second-worst performer is more likely to be experiencing normal market volatility suitable for a bounce-back trade.

## Features

- **Real-time Dashboard**: 4-column Windows Forms interface with live data
- **Top Coins Tracker**: Visual display of top 150 coins sorted by performance
- **Account Monitor**: Live balance updates and position tracking
- **System Logs**: Timestamped activity log with color-coded entries
- **Order Management**: View and track open orders with entry/TP/SL prices
- **Configurable Strategy**: Adjust universe size, thresholds, and exit parameters
- **Multiple Modes**: Paper trading (Demo), Testnet, and Live trading
- **Safety Features**: ReadOnly mode, automatic precision handling, rate limiting

## Requirements

- Windows OS
- .NET 8.0 Runtime
- Binance account with API keys (for Testnet or Live trading)

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/Fori99/Torra-Watch.git
   cd Torra-Watch
   ```

2. Build the project:
   ```bash
   cd torra_watch
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

## Configuration

### Environment Variables

Set your Binance API credentials based on the mode you want to use:

**Demo Mode** (Paper Trading - no keys required):
```bash
# No environment variables needed
```

**Testnet Mode**:
```bash
export BINANCE_API_KEY_TESTNET="your_testnet_api_key"
export BINANCE_API_SECRET_TESTNET="your_testnet_api_secret"
```

**Live Mode**:
```bash
export BINANCE_API_KEY_LIVE="your_live_api_key"
export BINANCE_API_SECRET_LIVE="your_live_api_secret"
```

### Strategy Parameters

Settings are stored in `%APPDATA%/TorraWatch/settings.json`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `Mode` | DEMO | Trading mode: DEMO, TESTNET, or LIVE |
| `ReadOnly` | true | If true, no actual orders are placed |
| `QuoteAsset` | USDT | Quote currency for trading pairs |
| `TopN` | 150 | Number of top coins to monitor (10-500) |
| `DropThresholdPct` | 4.0 | Minimum drop percentage to trigger entry (e.g., 4.0 = -4%) |
| `PickRank` | 2 | Which worst performer to pick (2 = second worst) |
| `TpPct` | 2.0 | Take profit percentage above entry |
| `SlPct` | 2.0 | Stop loss percentage below entry |
| `MaxHoldingMinutes` | 360 | Maximum position holding time (6 hours) |
| `ScanIntervalMin` | 1 | How often to refresh coin data (minutes) |
| `CooldownMinutes` | 5 | Wait time between trade cycles |
| `SymbolCooldownMin` | 90 | Avoid re-trading same coin (minutes) |
| `Blacklist` | [] | Array of symbols to never trade |

## Project Structure

```
torra_watch/
├── Core/                    # Trading logic and decision engine
│   ├── DecisionEngine.cs    # Entry signal evaluation
│   ├── RankingService.cs    # Coin ranking by performance
│   ├── StrategyConfig.cs    # Strategy parameters
│   └── BotSettings.cs       # Persistent configuration
├── Exchange/                # Exchange adapters
│   ├── BinanceHttpExchange.cs   # Binance REST API
│   └── PaperExchange.cs     # Simulated trading
├── Services/                # Data and account services
│   ├── BinanceMarketDataService.cs  # Market data fetching
│   ├── BinanceAccountService.cs     # Account operations
│   └── BinanceSignedClient.cs       # HTTP signing
├── Models/                  # Data models
├── Forms/                   # Windows Forms UI
│   └── MainForm.cs          # Main dashboard
├── UI/                      # UI controls and view models
│   ├── Controls/            # Custom UI controls
│   └── ViewModels/          # Data binding models
└── Program.cs               # Application entry point
```

## How It Works

### Trade Execution Flow

```
1. Bot Starts
   ↓
2. Fetch Top 150 Coins by Volume
   ↓
3. Calculate 3-Hour Returns for Each Coin
   ↓
4. Sort by Return (Worst to Best)
   ↓
5. Check if 2nd Worst ≤ -4%
   ├─ No  → Wait and retry
   └─ Yes → Continue
      ↓
6. Get Symbol Trading Rules (Precision)
   ↓
7. Check USDT Balance
   ↓
8. Place Market Buy (99% of balance)
   ↓
9. Wait for Order Settlement (3s)
   ↓
10. Place OCO Order (TP +2%, SL -2%)
    ↓
11. Wait for Exit (TP, SL, or 6h timeout)
    ↓
12. Cooldown Period
    ↓
13. Repeat from Step 2
```

### Key Components

**DecisionEngine**: Evaluates market conditions and determines when to enter a trade. Returns either a `CandidateFound` decision with the target symbol or a `Cooldown` signal.

**RankingService**: Builds the coin universe by fetching top coins by volume, then calculates 3-hour returns using historical kline data. Uses parallel requests (12 concurrent) with rate limiting.

**BinanceHttpExchange**: Handles all Binance API communication including:
- Public endpoints: prices, klines, exchange info
- Signed endpoints: orders, account balance
- Automatic time synchronization
- HMAC-SHA256 request signing

## Safety Features

1. **ReadOnly Mode**: Default enabled - simulates trades without execution
2. **API Key Validation**: Auto-enables ReadOnly if keys are missing
3. **Precision Handling**: Automatic rounding to exchange LOT_SIZE/PRICE_FILTER
4. **Rate Limiting**: Semaphore-limited concurrent requests (12 max)
5. **Error Resilience**: Per-symbol error handling - continues if one coin fails
6. **Balance Verification**: Retries up to 3 times to confirm order execution
7. **Symbol Cooldown**: Prevents re-trading same coin within 90 minutes

## Risk Warning

**This software is for educational purposes. Cryptocurrency trading involves substantial risk of loss.**

- Never trade with money you cannot afford to lose
- Past performance does not guarantee future results
- Test thoroughly on Testnet before using real funds
- Start with ReadOnly mode to verify behavior
- The authors are not responsible for any financial losses

## Dependencies

- `Binance.Net` (v11.7.1) - Binance API client
- `CryptoExchange.Net` (v9.7.0) - Exchange base library
- `Microsoft.Extensions.Hosting` (v9.0.9) - Dependency injection
- `Microsoft.Extensions.Logging.Console` (v9.0.9) - Logging
- `System.Text.Json` (v9.0.9) - JSON serialization

## License

This project is provided as-is for educational purposes.
