using torra_watch.Models;

namespace torra_watch.Services
{
    internal interface IMarketDataService
    {
        Task<Dictionary<string, decimal>> GetSpotPricesAsync(IEnumerable<string> symbols, CancellationToken ct);
        Task<IReadOnlyList<Models.Candle>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct);
        Task<IReadOnlyList<TopMove>> GetTopMoversAsync(int universe, TimeSpan lookback, CancellationToken ct);
        Task<SymbolInfo?> GetExchangeInfoAsync(string symbol, CancellationToken ct);
    }

    public class SymbolInfo
    {
        public string Symbol { get; set; } = "";
        public int QuantityPrecision { get; set; }
        public int PricePrecision { get; set; }
        public decimal MinQty { get; set; }
        public decimal MaxQty { get; set; }
        public decimal StepSize { get; set; }
        public decimal TickSize { get; set; }      // ⭐ ADD THIS
        public decimal MinPrice { get; set; }      // ⭐ ADD THIS
        public decimal MaxPrice { get; set; }      // ⭐ ADD THIS
        public decimal MinNotional { get; set; }
    }
}
