using torra_watch.Models;

namespace torra_watch.Services
{
    internal interface ITradingService
    {
        Task<OrderResult> PlaceMarketOrderAsync(string symbol, decimal qty, Models.Side side, CancellationToken ct);
        Task CancelAllAsync(string symbol, CancellationToken ct);
    }
}
