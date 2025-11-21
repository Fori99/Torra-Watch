using torra_watch.Core;

namespace torra_watch.Exchange;
public class PaperExchange
{
    private readonly Dictionary<string, decimal> _lastPrices = new();
    private readonly Dictionary<string, decimal> _price3hAgo = new();
    private readonly HashSet<string> _openPositions = new();
    private readonly Dictionary<string, (decimal tp, decimal sl)> _oco = new();

    private decimal _equity = 1000m; // starting paper equity
    private readonly Random _rng = new();
    private readonly object _lock = new();

    // --- Config ---
    private const decimal FeePctRoundTrip = 0.0020m; // 0.20% assumed taker round-trip

    public Task<IReadOnlyList<TickerInfo>> GetTopByQuoteVolumeAsync(int n = 150, CancellationToken ct = default)
    {
        // create/update dummy symbols with evolving prices
        for (int i = 1; i <= n; i++)
        {
            var sym = $"COIN{i}USDT";
            if (!_lastPrices.ContainsKey(sym))
            {
                var last = 1m + (decimal)_rng.NextDouble() * 100m;
                _lastPrices[sym] = last;
                _price3hAgo[sym] = last * (1m + (decimal)(_rng.NextDouble() * 0.1 - 0.05)); // +/-5%
            }
            else
            {
                // small random walk so OCO can hit
                var mid = _lastPrices[sym];
                var step = (decimal)(_rng.NextDouble() * 0.004 - 0.002); // +/- 0.2%
                _lastPrices[sym] = Math.Max(0.0000001m, mid * (1m + step));
            }
        }

        var list = Enumerable.Range(1, n).Select(i =>
        {
            var sym = $"COIN{i}USDT";
            var last = _lastPrices[sym];
            return new TickerInfo(sym, last, 10_000_000m + i * 100_000m, 5m);
        }).ToList();

        return Task.FromResult<IReadOnlyList<TickerInfo>>(list);
    }

    public Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult(_lastPrices.TryGetValue(symbol, out var p) ? p : 1m);

    public Task<decimal?> GetPriceAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
        => Task.FromResult<decimal?>(_price3hAgo.TryGetValue(symbol, out var p) ? p : null);

    public Task<(decimal bid, decimal ask)> GetTopOfBookAsync(string symbol, CancellationToken ct = default)
    {
        var mid = _lastPrices.TryGetValue(symbol, out var p) ? p : 1m;
        return Task.FromResult((mid * 0.999m, mid * 1.001m));
    }

    public Task<decimal> GetEquityAsync(CancellationToken ct = default) => Task.FromResult(_equity);

    public Task<string> MarketBuyAsync(string symbol, decimal quantity, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _openPositions.Add(symbol);
        }
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public Task PlaceOcoAsync(
     string symbol,
     decimal quantity,                 // <-- added to match IExchange
     decimal takeProfitPrice,
     decimal stopLossPrice,
     CancellationToken ct = default)
    {
        // store TP/SL for the symbol; quantity is not needed in paper mode
        lock (_lock)
        {
            _oco[symbol] = (takeProfitPrice, stopLossPrice);
        }
        return Task.CompletedTask;
    }
    public Task<bool> HasOpenPositionAsync(string symbol, CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_openPositions.Contains(symbol));
    }

    public Task ClosePositionAsync(string symbol, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _openPositions.Remove(symbol);
            _oco.Remove(symbol);
        }
        return Task.CompletedTask;
    }

    public Task<(decimal stepSize, decimal minNotional)> GetSymbolRulesAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult((0.000001m, 5m));

    // Apply a closed trade’s PnL to equity (includes fees)
    public void ApplyPnL(decimal pnlQuote, decimal feesPctRoundTrip = FeePctRoundTrip)
    {
        var fees = Math.Abs(_equity * feesPctRoundTrip); // rough: fees grow with equity; OK for paper
        _equity += pnlQuote - fees;
    }

    private void NudgePrice(string symbol)
    {
        if (!_lastPrices.TryGetValue(symbol, out var p) || p <= 0m) return;
        // random step ~ +/-0.25% so 2% bands get hit in a few seconds for testing
        var step = (decimal)(_rng.NextDouble() * 0.005 - 0.0025);
        var next = p * (1m + step);
        _lastPrices[symbol] = Math.Max(0.0000001m, next);
    }

    public async Task<(bool hit, ExitReason reason, decimal exitPrice, DateTime exitTimeUtc)> WaitForExitAsync(
        string symbol, DateTime entryTimeUtc, decimal entryPrice, TimeSpan timeStop, CancellationToken ct = default)
    {
        var deadline = entryTimeUtc + timeStop;

        while (!ct.IsCancellationRequested)
        {
            // simulate a small price move each tick so exits can trigger
            lock (_lock) { NudgePrice(symbol); }

            (decimal tp, decimal sl) levels;
            decimal pxNow;
            lock (_lock)
            {
                pxNow = _lastPrices.TryGetValue(symbol, out var p) ? p : entryPrice;
                levels = _oco.TryGetValue(symbol, out var l) ? l : (0m, 0m);
            }

            if (levels.tp > 0m && pxNow >= levels.tp)
                return (true, ExitReason.TakeProfit, pxNow, DateTime.UtcNow);

            if (levels.sl > 0m && pxNow <= levels.sl)
                return (true, ExitReason.StopLoss, pxNow, DateTime.UtcNow);

            if (DateTime.UtcNow >= deadline)
                return (true, ExitReason.TimeStop, pxNow, DateTime.UtcNow);

            await Task.Delay(1000, ct); // 1s tick
        }
        return (false, ExitReason.TimeStop, entryPrice, DateTime.UtcNow);
    }

}
