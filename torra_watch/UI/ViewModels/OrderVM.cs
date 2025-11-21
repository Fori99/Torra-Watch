namespace torra_watch.UI.ViewModels
{
    public class OrderVM
    {
        public string Symbol { get; init; } = "";
        public decimal LastPrice { get; init; }
        public decimal Buy { get; init; }
        public decimal TakeProfit { get; init; }
        public decimal StopLoss { get; init; }
        public string QuoteSymbol { get; init; } = "$"; // shown before numbers
    }
}
