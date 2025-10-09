using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Core
{    
    public sealed class ExchangeConfig
    {
        public bool UseBinance { get; set; } = true;
        public bool ReadOnly { get; set; } = false;
        public bool UseTestnet { get; set; } = false;  // leave false when using Demo
        public bool UseDemo { get; set; } = true;   // <— NEW: set true for demo.binance.com
        public string QuoteAsset { get; set; } = "USDT";

        public string? ApiKey { get; set; }  // your DEMO API key
        public string? ApiSecret { get; set; }  // your DEMO API secret
    }

}
