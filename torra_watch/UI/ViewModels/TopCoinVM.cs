namespace torra_watch.UI.ViewModels
{
    public class TopCoinVM
    {
        public string Symbol { get; init; } = "";   // e.g., "BTC"
        public decimal Price { get; init; }         // latest price in USDT
        public decimal ChangePct { get; init; }     // +2.5, -0.5 etc (window-based)
        public decimal? PrevPrice { get; set; }
        public Image Icon { get; set; }
    }
}
