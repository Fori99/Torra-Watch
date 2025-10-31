using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    public class AccountSnapshot
    {
        public decimal TotalUsdt { get; init; }
        public IReadOnlyList<Balance> Balances { get; init; } = Array.Empty<Balance>();
    }
}
