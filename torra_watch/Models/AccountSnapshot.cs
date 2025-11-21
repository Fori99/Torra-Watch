namespace torra_watch.Models
{
    public class AccountSnapshot
    {
        public decimal TotalUsdt { get; init; }
        public IReadOnlyList<Balance> Balances { get; init; } = Array.Empty<Balance>();
    }
}
