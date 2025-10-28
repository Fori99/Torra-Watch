using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.UI.ViewModels
{
    public sealed class AccountVM
    {
        public decimal MainUsdt { get; init; }                       // total USDT (free + locked)
        public IReadOnlyList<HoldingVM> OtherHoldings { get; init; } // all non-USDT assets
        public decimal TotalUsdt => MainUsdt + (OtherHoldings?.Sum(h => h.EstUsdt) ?? 0m);
    }

    public sealed class HoldingVM
    {
        public string Asset { get; init; } = "";
        public decimal Free { get; init; }
        public decimal Locked { get; init; }
        public decimal Total => Free + Locked;
        public decimal EstUsdt { get; init; }       // estimated value in USDT
        public decimal PercentOfTotal { get; set; } // relative to AccountVM.TotalUsdt (0..100)
    }
}
