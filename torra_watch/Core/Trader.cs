//namespace torra_watch.Core
//{
//    public sealed class Trader
//    {
//        private readonly IExchange _ex;
//        private readonly RankingService _ranking;
//        private readonly DecisionEngine _engine;
//        private readonly StrategyConfig _cfg;
//        private readonly ExchangeConfig _exCfg;

//        private string? _lastSymbol; // "don’t buy the same coin twice" rule

//        public Trader(IExchange ex, RankingService ranking, DecisionEngine engine, StrategyConfig cfg, ExchangeConfig exCfg)
//        {
//            _ex = ex;
//            _ranking = ranking;
//            _engine = engine;
//            _cfg = cfg;
//            _exCfg = exCfg;
//        }

//        public async Task<(bool entered, string? symbol, string note)> TryEnterAsync(CancellationToken ct = default)
//        {
//            var decision = await _engine.DecideAsync(ct);
//            if (decision.kind != DecisionKind.CandidateFound || string.IsNullOrWhiteSpace(decision.symbol))
//                return (false, null, decision.note);

//            if (_lastSymbol is not null && decision.symbol == _lastSymbol)
//                return (false, null, "Skip: same symbol as previous trade.");

//            var symbol = decision.symbol;

//            // Current mid for sizing and TP/SL anchors
//            var (bid, ask) = await _ex.GetTopOfBookAsync(symbol, ct);
//            var entryMid = (bid + ask) / 2m;
//            if (entryMid <= 0m) return (false, null, $"Skip: no valid market price for {symbol}.");

//            // Rules for min notional and qty step
//            var (stepSize, minNotional) = await _ex.GetSymbolRulesAsync(symbol, ct);

//            // Equity & spend calculation
//            var equity = await _ex.GetEquityAsync(ct);
//            if (equity <= 0m) return (false, null, "Skip: equity=0.");

//            // RoundDown helper for quote amounts (e.g., USDT typically 2 decimals)
//            static decimal RoundDown(decimal v, int decimals)
//            {
//                var scale = (decimal)Math.Pow(10, decimals);
//                return Math.Floor(v * scale) / scale;
//            }

//            // Spend ~99% of equity but not less than 1.05 × minNotional (small safety margin)
//            var spend = RoundDown(Math.Max(minNotional * 1.05m, equity * 0.99m), 2);
//            if (spend < minNotional)
//                return (false, null, $"Skip: spend {spend:0.##} < minNotional {minNotional:0.##}.");

//            // ---- Place MARKET BUY using quote notional (preferred path on Binance HTTP) ----
//            string orderId = "N/A";
//            decimal filledBaseQty = 0m;

//            try
//            {
//                // If our adapter is the HTTP Binance one, use the helper that returns executed qty
//                if (_ex is torra_watch.Exchange.BinanceHttpExchange http)
//                {
//                    var (id, executedQty) = await http.MarketBuyWithQuoteAsync(symbol, spend, ct);
//                    orderId = id;
//                    filledBaseQty = executedQty;
//                }
//                else
//                {
//                    // Generic path: fall back to interface MarketBuyAsync (interprets arg as quote notional in our app)
//                    orderId = await _ex.MarketBuyAsync(symbol, spend, ct);
//                    // Approximate base qty if the adapter can't return it
//                    filledBaseQty = spend / entryMid;
//                }
//            }
//            catch (Exception ex)
//            {
//                return (false, null, $"Buy error: {ex.Message}");
//            }

//            // Round base qty to step size and guard against 0 after rounding
//            static decimal RoundToStep(decimal qty, decimal step)
//                => step <= 0 ? qty : Math.Floor(qty / step) * step;

//            var qtyForOco = RoundToStep(filledBaseQty, stepSize);
//            if (qtyForOco <= 0m)
//                return (true, symbol, $"Bought {symbol} spending {spend:0.##} {(_exCfg?.QuoteAsset ?? "USDT")}, orderId={orderId}, but qty≈0 by LOT_SIZE; OCO skipped.");

//            // Compute TP/SL from the entry mid
//            var tp = entryMid * (1m + _cfg.TakeProfitPct);
//            var sl = entryMid * (1m - _cfg.StopLossPct);

//            // If we can, round TP/SL to tick size precisely (optional, only if HTTP adapter is available)
//            try
//            {
//                if (_ex is torra_watch.Exchange.BinanceHttpExchange http2)
//                {
//                    var rules = await http2.GetSymbolRulesDetailedAsync(symbol, ct);
//                    decimal RoundToTick(decimal px) => rules.TickSize <= 0 ? px : Math.Floor(px / rules.TickSize) * rules.TickSize;
//                    tp = RoundToTick(tp);
//                    sl = RoundToTick(sl);
//                }
//            }
//            catch
//            {
//                // If filter fetch fails, we still proceed with unrounded TP/SL (the OCO call will validate)
//            }

//            // Place OCO (adapter will enforce notional/precision; bubble any error in note)
//            try
//            {
//                await _ex.PlaceOcoAsync(symbol, qtyForOco, tp, sl, ct);
//            }
//            catch (Exception ex)
//            {
//                return (true, symbol, $"Bought {symbol} spend={spend:0.##}, qty≈{qtyForOco:0.######}, orderId={orderId}. OCO error: {ex.Message}");
//            }

//            _lastSymbol = symbol;

//            var q = _exCfg?.QuoteAsset ?? "USDT";
//            return (true, symbol,
//                $"Entered {symbol} @~{entryMid:0.########}, spent {spend:0.##} {q}, qty≈{qtyForOco:0.######}, TP {tp:0.########}, SL {sl:0.########} (orderId={orderId}).");
//        }

//        // Wait for exit and settle in paper mode; for live, we just force-close if called.
//        public async Task<TradeOutcome?> WaitAndSettleAsync(string symbol, DateTime entryTimeUtc, decimal entryPrice, decimal qty, CancellationToken ct = default)
//        {
//            // Paper mode path (simulated exits)
//            if (_ex is torra_watch.Exchange.PaperExchange paper)
//            {
//                var (hit, reason, exitPx, exitTs) = await paper.WaitForExitAsync(
//                    symbol, entryTimeUtc, entryPrice, TimeSpan.FromHours(_cfg.TimeStopHours), ct);

//                if (!hit) return null;

//                await _ex.ClosePositionAsync(symbol, ct);

//                var pnl = (exitPx - entryPrice) * qty;
//                paper.ApplyPnL(pnl);

//                return new TradeOutcome(symbol, entryPrice, exitPx, qty, entryTimeUtc, exitTs, reason, 0.0020m, pnl);
//            }

//            // Live path (Binance): normally OCO handles exit. If we got here, force close.
//            await _ex.ClosePositionAsync(symbol, ct);
//            return null;
//        }
//    }
//}
