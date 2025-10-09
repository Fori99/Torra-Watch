namespace torra_watch.Core;

public interface IExchange
{
    // Market data
    Task<IReadOnlyList<TickerInfo>> GetTopByQuoteVolumeAsync(int n = 150, CancellationToken ct = default);
    Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct = default);
    Task<decimal?> GetPriceAtAsync(string symbol, DateTime utc, CancellationToken ct = default);
    Task<(decimal bid, decimal ask)> GetTopOfBookAsync(string symbol, CancellationToken ct = default);

    // Account & trading
    Task<decimal> GetEquityAsync(CancellationToken ct = default);
    Task<string> MarketBuyAsync(string symbol, decimal quantity, CancellationToken ct = default);
    Task PlaceOcoAsync(string symbol, decimal quantity, decimal takeProfitPrice, decimal stopLossPrice, CancellationToken ct = default);
    Task<bool> HasOpenPositionAsync(string symbol, CancellationToken ct = default);
    Task ClosePositionAsync(string symbol, CancellationToken ct = default);

    // Trading rules for symbol (qty step, min notional)
    Task<(decimal stepSize, decimal minNotional)> GetSymbolRulesAsync(string symbol, CancellationToken ct = default);


}
