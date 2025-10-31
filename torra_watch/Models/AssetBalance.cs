using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    public class AssetBalance
    {
        public string Asset { get; set; } = "";
        public decimal Free { get; set; }
        public decimal Locked { get; set; }
        public decimal EstUsdt { get; set; }
    }
}
