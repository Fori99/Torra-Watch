using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using torra_watch.Core;
using torra_watch.Models;

namespace torra_watch.Services
{
    internal interface ITradingService
    {
        Task<OrderResult> PlaceMarketOrderAsync(string symbol, decimal qty, Models.Side side, CancellationToken ct);
        Task CancelAllAsync(string symbol, CancellationToken ct);
    }
}
