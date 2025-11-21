namespace torra_watch.Models
{
    public class OpenOrder
    {
        public string Symbol { get; init; } = "";
        public string Side { get; init; } = "";     // BUY/SELL
        public string Type { get; init; } = "";     // LIMIT/MARKET/OCO leg etc.
        public decimal Price { get; init; }
        public decimal OrigQty { get; init; }
        public decimal ExecutedQty { get; init; }
        public DateTime TimeUtc { get; init; }
    }
}
