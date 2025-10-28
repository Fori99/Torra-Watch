using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    internal class TopMove
    {
        public string Symbol { get; init; } = "";
        public decimal Now { get; init; }
        public decimal PriceAgo { get; init; }
        public decimal ChangePct { get; init; }     // (+/-)
        public TopMove(string symbol, decimal now, decimal priceAgo, decimal changePct)
            => (Symbol, Now, PriceAgo, ChangePct) = (symbol, now, priceAgo, changePct);
    }
}
