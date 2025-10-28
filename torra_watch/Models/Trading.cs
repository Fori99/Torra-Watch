using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace torra_watch.Models
{
    public enum Side { Buy, Sell }
    internal class OrderResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
    }
}
