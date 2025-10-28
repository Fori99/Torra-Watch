using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    internal class Position
    {
        public string Symbol { get; init; } = "";
        public decimal Price { get; init; }
        public decimal TakeProfit { get; init; }
        public decimal StopLoss { get; init; }
    }
}
