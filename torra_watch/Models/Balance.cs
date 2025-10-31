using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    public class Balance
    {
        public string Asset { get; init; } = "";
        public decimal Qty { get; init; }
        public decimal EstUsdt { get; init; }
    }
}
