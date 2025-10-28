using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using torra_watch.Models;

namespace torra_watch.Services
{
    internal class BinanceMarketDataService : IMarketDataService
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

        public BinanceMarketDataService(HttpClient http)
        {
            _http = http;
            _http.BaseAddress ??= new Uri("https://api.binance.com");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<Dictionary<string, decimal>> GetSpotPricesAsync(IEnumerable<string> symbols, CancellationToken ct)
        {
            // /api/v3/ticker/price returns all; filter locally to avoid rate-limit spam.
            var all = await _http.GetFromJsonAsync<List<TickerPrice>>("/api/v3/ticker/price", J, ct)
                      ?? new List<TickerPrice>();
            var set = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return all.Where(t => set.Contains(t.symbol))
                      .ToDictionary(t => t.symbol, t => t.price);
        }

        public async Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct)
        {
            // /api/v3/klines?symbol=BTCUSDT&interval=1m&limit=180
            var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadFromJsonAsync<List<object[]>>(J, ct) ?? new();
            return raw.Select(r => new Candle
            {
                OpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(r[0]!)).UtcDateTime,
                Open = Convert.ToDecimal(r[1]!, System.Globalization.CultureInfo.InvariantCulture),
                High = Convert.ToDecimal(r[2]!, System.Globalization.CultureInfo.InvariantCulture),
                Low = Convert.ToDecimal(r[3]!, System.Globalization.CultureInfo.InvariantCulture),
                Close = Convert.ToDecimal(r[4]!, System.Globalization.CultureInfo.InvariantCulture),
                Volume = Convert.ToDecimal(r[5]!, System.Globalization.CultureInfo.InvariantCulture),
            }).ToList();
        }

        public async Task<IReadOnlyList<TopMove>> GetTopMoversAsync(int universe, TimeSpan lookback, CancellationToken ct)
        {
            // 1) get all USDT spot pairs; simplest: read /exchangeInfo once and cache (optional).
            var info = await _http.GetFromJsonAsync<ExchangeInfo>("/api/v3/exchangeInfo", J, ct) ?? new();
            var symbols = info.symbols.Where(s => s.quoteAsset == "USDT" && s.status == "TRADING")
                                      .Select(s => s.symbol).ToArray();

            // 2) get current prices once
            var allTicker = await _http.GetFromJsonAsync<List<TickerPrice>>("/api/v3/ticker/price", J, ct) ?? new();
            var nowMap = allTicker.Where(t => symbols.Contains(t.symbol))
                                  .ToDictionary(t => t.symbol, t => t.price, StringComparer.OrdinalIgnoreCase);

            var barsNeeded = Math.Max(1, (int)Math.Ceiling(lookback.TotalMinutes));
            var list = new List<TopMove>(symbols.Length);

            foreach (var s in symbols)
            {
                // Keep it simple first: 1m bars window
                var kl = await GetKlinesAsync(s, "1m", barsNeeded + 1, ct);
                if (kl.Count == 0 || !nowMap.TryGetValue(s, out var now)) continue;
                var priceAgo = kl.First().Close;
                if (priceAgo == 0) continue;
                var change = (now - priceAgo) / priceAgo * 100m;
                list.Add(new TopMove(s, now, priceAgo, Math.Round(change, 2)));
            }

            return list.OrderByDescending(x => Math.Abs(x.ChangePct))
                       .Take(universe)
                       .ToList();
        }

        // --- DTOs for deserialization ---
        private sealed record TickerPrice(string symbol, decimal price);
        private sealed record ExchangeInfo(List<ExSymbol> symbols)
        {
            public ExchangeInfo() : this(new List<ExSymbol>()) { }
        }

        private sealed record ExSymbol(string symbol, string status, string baseAsset, string quoteAsset);
    }
}