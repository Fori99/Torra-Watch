using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Core
{
    public enum ExitReason { TakeProfit, StopLoss, TimeStop }

    public sealed record TradeOutcome(
        string Symbol,
        decimal EntryPrice,
        decimal ExitPrice,
        decimal Quantity,
        DateTime EntryTimeUtc,
        DateTime ExitTimeUtc,
        ExitReason Reason,
        decimal FeesPctRoundTrip,
        decimal PnL // in quote currency (e.g., USDT)
    );
}
