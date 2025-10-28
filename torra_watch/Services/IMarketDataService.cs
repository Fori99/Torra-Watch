using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using torra_watch.Core;
using torra_watch.Models;

namespace torra_watch.Services
{
    internal interface IMarketDataService
    {
        Task<Dictionary<string, decimal>> GetSpotPricesAsync(IEnumerable<string> symbols, CancellationToken ct);
        Task<IReadOnlyList<Models.Candle>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct);
        Task<IReadOnlyList<TopMove>> GetTopMoversAsync(int universe, TimeSpan lookback, CancellationToken ct);
    }
}
