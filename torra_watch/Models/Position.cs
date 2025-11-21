namespace torra_watch.Models
{
    public class Position
    {
        public string Symbol { get; init; } = "";
        public decimal Price { get; init; }
        public decimal TakeProfit { get; init; }
        public decimal StopLoss { get; init; }
    }
}
