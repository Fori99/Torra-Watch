using torra_watch.Models;


namespace torra_watch.Services
{
    public interface IAccountService
    {/// <summary>
     /// Get account balances and total USDT value
     /// </summary>
        Task<AccountSnapshot> GetBalancesAsync(CancellationToken ct);

        /// <summary>
        /// Get all open orders
        /// </summary>
        Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsync(CancellationToken ct);

        /// <summary>
        /// Place a market buy order
        /// </summary>
        /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT")</param>
        /// <param name="quantity">Quantity to buy</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Order result with executed quantity and average price</returns>
        Task<BuyOrderResult> PlaceMarketBuyAsync(string symbol, decimal quantity, CancellationToken ct);

        /// <summary>
        /// Place an OCO (One-Cancels-Other) order
        /// </summary>
        /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT")</param>
        /// <param name="quantity">Quantity to sell</param>
        /// <param name="takeProfitPrice">Take profit limit price</param>
        /// <param name="stopLossPrice">Stop loss trigger price</param>
        /// <param name="stopLimitPrice">Stop limit price (must be <= stopLossPrice)</param>
        /// <param name="ct">Cancellation token</param>
        Task PlaceOcoOrderAsync(
            string symbol,
            decimal quantity,
            decimal takeProfitPrice,
            decimal stopLossPrice,
            decimal stopLimitPrice,
            CancellationToken ct);

        /// <summary>
        /// Get the free balance for a specific asset
        /// </summary>
        /// <param name="asset">Asset symbol (e.g., "BTC", "ETH")</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Free balance available for trading</returns>
        Task<decimal> GetAssetBalanceAsync(string asset, CancellationToken ct);

        /// <summary>
        /// Place a market sell order for the entire balance of an asset.
        /// Gets actual balance, rounds down to stepSize, and sells maximum possible.
        /// </summary>
        /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT")</param>
        /// <param name="symbolInfo">Symbol trading rules for quantity adjustment</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Sell order result with executed quantity and price</returns>
        Task<SellOrderResult> MarketSellEntireBalanceAsync(
            string symbol,
            SymbolInfo symbolInfo,
            CancellationToken ct);
    }

    /// <summary>
    /// Result of a market sell order
    /// </summary>
    public class SellOrderResult
    {
        public string Symbol { get; set; } = "";
        public decimal ExecutedQty { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal DustRemaining { get; set; }
        public string OrderId { get; set; } = "";
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of a market buy order
    /// </summary>
    public class BuyOrderResult
    {
        public string Symbol { get; set; } = "";
        public decimal ExecutedQty { get; set; }
        public decimal AvgPrice { get; set; }
        public string OrderId { get; set; } = "";
    }

    /// <summary>
    /// Account balance snapshot
    /// </summary>
    public class AccountSnapshot
    {
        public decimal TotalUsdt { get; set; }
        public List<Balance> Balances { get; set; } = new();
    }

}
