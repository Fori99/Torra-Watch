namespace torra_watch.Services
{
    /// <summary>
    /// Helper class for adjusting quantities and prices to match Binance exchange filters.
    /// Prevents dust by properly rounding to LOT_SIZE stepSize.
    /// </summary>
    public static class QuantityHelper
    {
        /// <summary>
        /// Rounds a quantity DOWN to the nearest valid stepSize.
        /// Example: If stepSize is 0.01 and quantity is 1.2345, returns 1.23
        /// </summary>
        public static decimal FloorToStepSize(decimal quantity, decimal stepSize)
        {
            if (stepSize <= 0) return quantity;
            return Math.Floor(quantity / stepSize) * stepSize;
        }

        /// <summary>
        /// Rounds a price DOWN to the nearest valid tickSize.
        /// </summary>
        public static decimal FloorToTickSize(decimal price, decimal tickSize)
        {
            if (tickSize <= 0) return price;
            return Math.Floor(price / tickSize) * tickSize;
        }

        /// <summary>
        /// Rounds a price UP to the nearest valid tickSize (for take profit).
        /// </summary>
        public static decimal CeilToTickSize(decimal price, decimal tickSize)
        {
            if (tickSize <= 0) return price;
            return Math.Ceiling(price / tickSize) * tickSize;
        }

        /// <summary>
        /// Adjusts quantity for a BUY order:
        /// - Floors to stepSize
        /// - Validates >= minQty
        /// - Validates notional (qty * price) >= minNotional
        /// </summary>
        /// <returns>Adjusted quantity, or null if order cannot meet minimums</returns>
        public static AdjustedQuantityResult AdjustBuyQuantity(
            decimal rawQuantity,
            decimal currentPrice,
            SymbolInfo symbolInfo)
        {
            var stepSize = symbolInfo.StepSize;
            var minQty = symbolInfo.MinQty;
            var minNotional = symbolInfo.MinNotional;

            // Round DOWN to stepSize
            var adjustedQty = FloorToStepSize(rawQuantity, stepSize);

            // Check minimum quantity
            if (adjustedQty < minQty)
            {
                return AdjustedQuantityResult.Failure(
                    $"Quantity {adjustedQty} is below minimum {minQty}");
            }

            // Check minimum notional
            var notional = adjustedQty * currentPrice;
            if (notional < minNotional)
            {
                return AdjustedQuantityResult.Failure(
                    $"Notional {notional:F2} USDT is below minimum {minNotional:F2} USDT");
            }

            return AdjustedQuantityResult.Success(adjustedQty, notional);
        }

        /// <summary>
        /// Adjusts quantity for a SELL order (to sell entire balance without dust):
        /// - Gets actual balance
        /// - Floors to stepSize
        /// - Returns maximum sellable amount
        /// </summary>
        /// <returns>Adjusted quantity, or null if cannot sell</returns>
        public static AdjustedQuantityResult AdjustSellQuantity(
            decimal actualBalance,
            decimal currentPrice,
            SymbolInfo symbolInfo)
        {
            var stepSize = symbolInfo.StepSize;
            var minQty = symbolInfo.MinQty;
            var minNotional = symbolInfo.MinNotional;

            // Round DOWN to stepSize - this maximizes what we can sell
            var adjustedQty = FloorToStepSize(actualBalance, stepSize);

            if (adjustedQty <= 0)
            {
                return AdjustedQuantityResult.Failure(
                    $"Balance {actualBalance} rounds to zero with stepSize {stepSize}");
            }

            // Check minimum quantity
            if (adjustedQty < minQty)
            {
                return AdjustedQuantityResult.Failure(
                    $"Sellable quantity {adjustedQty} is below minimum {minQty}");
            }

            // Check minimum notional
            var notional = adjustedQty * currentPrice;
            if (notional < minNotional)
            {
                return AdjustedQuantityResult.Failure(
                    $"Notional {notional:F2} USDT is below minimum {minNotional:F2} USDT");
            }

            // Calculate dust left after selling
            var dustRemaining = actualBalance - adjustedQty;

            return AdjustedQuantityResult.Success(adjustedQty, notional, dustRemaining);
        }

        /// <summary>
        /// Calculates take profit and stop loss prices, properly rounded.
        /// </summary>
        public static (decimal takeProfit, decimal stopLoss, decimal stopLimit) CalculateExitPrices(
            decimal entryPrice,
            decimal takeProfitPct,
            decimal stopLossPct,
            SymbolInfo symbolInfo)
        {
            var tickSize = symbolInfo.TickSize;

            // Take profit should be rounded UP (ceil) to ensure we get at least our target
            var rawTp = entryPrice * (1 + takeProfitPct / 100m);
            var takeProfit = CeilToTickSize(rawTp, tickSize);

            // Stop loss should be rounded DOWN (floor) to trigger sooner if needed
            var rawSl = entryPrice * (1 - stopLossPct / 100m);
            var stopLoss = FloorToTickSize(rawSl, tickSize);

            // Stop limit should be slightly below stop loss
            var stopLimit = FloorToTickSize(stopLoss * 0.999m, tickSize);

            // Ensure stop limit doesn't go below min price
            if (stopLimit < symbolInfo.MinPrice)
            {
                stopLimit = symbolInfo.MinPrice;
            }

            return (takeProfit, stopLoss, stopLimit);
        }
    }

    /// <summary>
    /// Result of quantity adjustment operation.
    /// </summary>
    public class AdjustedQuantityResult
    {
        public bool IsSuccess { get; }
        public decimal Quantity { get; }
        public decimal Notional { get; }
        public decimal DustRemaining { get; }
        public string? ErrorMessage { get; }

        private AdjustedQuantityResult(bool success, decimal quantity, decimal notional, decimal dust, string? error)
        {
            IsSuccess = success;
            Quantity = quantity;
            Notional = notional;
            DustRemaining = dust;
            ErrorMessage = error;
        }

        public static AdjustedQuantityResult Success(decimal quantity, decimal notional, decimal dust = 0)
            => new(true, quantity, notional, dust, null);

        public static AdjustedQuantityResult Failure(string error)
            => new(false, 0, 0, 0, error);
    }
}
