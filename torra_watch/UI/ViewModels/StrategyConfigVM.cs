using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.UI.ViewModels
{
    public class StrategyConfigVM
    {
        public int UniverseSize { get; init; }
        public decimal MinDrop3hPct { get; init; }       // keep if you use it elsewhere
        public decimal TakeProfitPct { get; init; }
        public decimal StopLossPct { get; init; }       // NEW (default 3)
        public int CooldownMinutes { get; init; }        // NEW (default 10)
        public decimal SecondWorstMinDropPct { get; init; } // NEW (default 4)

        public static StrategyConfigVM Defaults() => new()
        {
            UniverseSize = 150,
            MinDrop3hPct = 4.00m,
            TakeProfitPct = 2.0m,
            StopLossPct = 2.0m,
            CooldownMinutes = 5,
            SecondWorstMinDropPct = 4.00m
        };
    }
}
