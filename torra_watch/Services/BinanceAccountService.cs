using System.Net.Http.Json;
using System.Text.Json;
using torra_watch.Models;

namespace torra_watch.Services
{
    public class BinanceAccountService : IAccountService
    {
        private readonly BinanceSignedClient _signed;
        private readonly HttpClient _http; // public endpoints
        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

        public BinanceAccountService(BinanceSignedClient signed, HttpClient httpPublic)
        {
            _signed = signed;
            _http = httpPublic;
            _http.BaseAddress ??= new Uri("https://api.binance.com");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        // ---- IAccountService ----
        public async Task<AccountSnapshot> GetBalancesAsync(CancellationToken ct)
        {
            // 1) signed /api/v3/account -> balances
            var acc = await _signed.GetSignedAsync<AccountDto>("/api/v3/account", new Dictionary<string, string?>(), ct)
                      ?? new AccountDto();

            // 2) public /api/v3/ticker/price -> map for EstUsdt
            var tickers = await _http.GetFromJsonAsync<List<TickerPrice>>("/api/v3/ticker/price", J, ct)
                          ?? new List<TickerPrice>();

            // Build symbol->price for USDT pairs
            // Build symbol->price for USDT pairs
            var usdtMap = tickers
                .Where(t => t.symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(t => t.symbol, t => t.price, StringComparer.OrdinalIgnoreCase);

            // NOTE: use your existing Models.Balance, not AssetBalance
            var balances = new List<Balance>();

            foreach (var b in acc.balances)
            {
                var free = ToDec(b.free);
                var locked = ToDec(b.locked);
                var qty = free + locked;
                if (qty == 0m) continue;

                decimal estUsdt = 0m;
                if (!string.Equals(b.asset, "USDT", StringComparison.OrdinalIgnoreCase))
                {
                    var pair = b.asset + "USDT";
                    if (usdtMap.TryGetValue(pair, out var px))
                        estUsdt = qty * px;
                }
                else
                {
                    estUsdt = qty;
                }

                balances.Add(new Balance
                {
                    Asset = b.asset,
                    Qty = qty,       // <-- your model likely has Qty
                    EstUsdt = estUsdt
                });
            }

            var total = balances.Sum(x => x.EstUsdt);
            balances = balances.OrderByDescending(x => x.EstUsdt).ToList();

            return new AccountSnapshot
            {
                TotalUsdt = decimal.Round(total, 2, MidpointRounding.AwayFromZero),
                Balances = balances
            };

        }

        // If your interface expects only (CancellationToken), provide that signature
        public Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsync(CancellationToken ct)
            => GetOpenOrdersAsyncInternal(null, ct);

        // You can keep an internal method with symbol filtering if you need it
        private async Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsyncInternal(string? symbol, CancellationToken ct)
        {
            var q = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(symbol)) q["symbol"] = symbol;

            var raw = await _signed.GetSignedAsync<List<OpenOrderDto>>("/api/v3/openOrders", q, ct)
                      ?? new List<OpenOrderDto>();

            return raw.Select(o => new OpenOrder
            {
                Symbol = o.symbol,
                Side = o.side,
                Type = o.type,
                Price = ToDec(o.price),
                OrigQty = ToDec(o.origQty),
                ExecutedQty = ToDec(o.executedQty),
                TimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(o.time).UtcDateTime
            }).ToList();
        }

        // ---- DTOs (class, not record, so we get parameterless ctor + Web naming) ----
        private sealed class AccountDto
        {
            public List<Bal> balances { get; set; } = new();
        }

        private sealed class Bal
        {
            public string asset { get; set; } = "";
            public string free { get; set; } = "0";
            public string locked { get; set; } = "0";
        }

        private sealed class TickerPrice
        {
            public string symbol { get; set; } = "";
            public decimal price { get; set; }
        }

        private sealed class OpenOrderDto
        {
            public string symbol { get; set; } = "";
            public string side { get; set; } = "";
            public string type { get; set; } = "";
            public string price { get; set; } = "0";
            public string origQty { get; set; } = "0";
            public string executedQty { get; set; } = "0";
            public long time { get; set; }
        }

        private static decimal ToDec(string s) =>
            decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
