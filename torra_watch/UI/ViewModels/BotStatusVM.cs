using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.UI.ViewModels
{
    public enum BotPrimaryStatus { Stopped, Starting, Running, Stopping, Panic, Error, CoolingDown }
    public enum BotCycleStep
    {
        None = 0,
        GetTopCoins,
        SortByChange,
        PickSecondWorst,
        Buy,
        PlaceOco,
        WaitingToSell,
        Restart
    }

    public  class BotStatusVM
    {
        public BotPrimaryStatus Primary { get; init; } = BotPrimaryStatus.Stopped;
        public BotCycleStep Step { get; init; } = BotCycleStep.None;

        // Meta
        public string Mode { get; init; } = "Paper";
        public string Exchange { get; init; } = "Binance";

        // Counters
        public TimeSpan Uptime { get; init; } = TimeSpan.Zero;
        public int SignalsCount { get; init; }
        public int ActiveOrders { get; init; }

        // Ops flags
        public bool Connected { get; init; }
        public bool RateLimited { get; init; }
        public bool Degraded { get; init; }
        public bool MarketClosed { get; init; }

        // Cycle context (optional formatting)
        public int LookbackHours { get; init; }      // e.g., 3
        public decimal SecondWorstMinDropPct { get; init; } // e.g., 4
        public decimal TpPct { get; init; }          // e.g., 0.5
        public decimal SlPct { get; init; }          // e.g., 0.5
        public string? CurrentSymbol { get; init; }  // e.g., "SOL"

        // Cooldown
        public TimeSpan? CooldownRemaining { get; init; }

        public DateTime? LastCheckUtc { get; init; }
        public DateTime? NextCheckUtc { get; init; }
    }
}

