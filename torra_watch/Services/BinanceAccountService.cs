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
            _http.BaseAddress ??= new System.Uri("https://api.binance.com");
            _http.Timeout = System.TimeSpan.FromSeconds(15);
        }

        // ---- IAccountService Implementation ----

        public async Task<AccountSnapshot> GetBalancesAsync(CancellationToken ct)
        {
            // 1) signed /api/v3/account -> balances
            var acc = await _signed.GetSignedAsync<AccountDto>("/api/v3/account", new Dictionary<string, string?>(), ct)
                      ?? new AccountDto();

            // 2) public /api/v3/ticker/price -> map for EstUsdt
            var tickers = await _http.GetFromJsonAsync<List<TickerPrice>>("/api/v3/ticker/price", J, ct)
                          ?? new List<TickerPrice>();

            // Build symbol->price for USDT pairs
            var usdtMap = tickers
                .Where(t => t.symbol.EndsWith("USDT", System.StringComparison.OrdinalIgnoreCase))
                .ToDictionary(t => t.symbol, t => t.price, System.StringComparer.OrdinalIgnoreCase);

            var balances = new List<Balance>();

            foreach (var b in acc.balances)
            {
                var free = ToDec(b.free);
                var locked = ToDec(b.locked);
                var qty = free + locked;
                if (qty == 0m) continue;

                decimal estUsdt = 0m;
                if (!string.Equals(b.asset, "USDT", System.StringComparison.OrdinalIgnoreCase))
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
                    Qty = qty,
                    EstUsdt = estUsdt
                });
            }

            var total = balances.Sum(x => x.EstUsdt);
            balances = balances.OrderByDescending(x => x.EstUsdt).ToList();

            return new AccountSnapshot
            {
                TotalUsdt = decimal.Round(total, 2, System.MidpointRounding.AwayFromZero),
                Balances = balances
            };
        }

        public async Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsync(CancellationToken ct)
        {
            var raw = await _signed.GetSignedAsync<List<OpenOrderDto>>("/api/v3/openOrders", new Dictionary<string, string?>(), ct)
                      ?? new List<OpenOrderDto>();

            return raw.Select(o => new OpenOrder
            {
                Symbol = o.symbol,
                Side = o.side,
                Type = o.type,
                Price = ToDec(o.price),
                OrigQty = ToDec(o.origQty),
                ExecutedQty = ToDec(o.executedQty),
                TimeUtc = System.DateTimeOffset.FromUnixTimeMilliseconds(o.time).UtcDateTime
            }).ToList();
        }

        public async Task<BuyOrderResult> PlaceMarketBuyAsync(string symbol, decimal quantity, CancellationToken ct)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["symbol"] = symbol,
                ["side"] = "BUY",
                ["type"] = "MARKET",
                ["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            var response = await _signed.PostSignedAsync<OrderResponseDto>("/api/v3/order", parameters, ct);

            if (response == null)
                throw new System.Exception("Order response was null");

            // Calculate average price from fills
            decimal totalQty = 0m;
            decimal totalCost = 0m;

            foreach (var fill in response.fills ?? new List<FillDto>())
            {
                var fillQty = ToDec(fill.qty);
                var fillPrice = ToDec(fill.price);
                totalQty += fillQty;
                totalCost += fillQty * fillPrice;
            }

            var avgPrice = totalQty > 0 ? totalCost / totalQty : 0m;

            return new BuyOrderResult
            {
                Symbol = response.symbol,
                ExecutedQty = ToDec(response.executedQty),
                AvgPrice = avgPrice,
                OrderId = response.orderId.ToString()
            };
        }

        public async Task PlaceOcoOrderAsync(
            string symbol,
            decimal quantity,
            decimal takeProfitPrice,
            decimal stopLossPrice,
            decimal stopLimitPrice,
            CancellationToken ct)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["symbol"] = symbol,
                ["side"] = "SELL",
                ["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["price"] = takeProfitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["stopPrice"] = stopLossPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["stopLimitPrice"] = stopLimitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["stopLimitTimeInForce"] = "GTC"
            };

            await _signed.PostSignedAsync<OcoResponseDto>("/api/v3/order/oco", parameters, ct);
        }

        public async Task<decimal> GetAssetBalanceAsync(string asset, CancellationToken ct)
        {
            var acc = await _signed.GetSignedAsync<AccountDto>("/api/v3/account", new Dictionary<string, string?>(), ct)
                      ?? new AccountDto();

            var balance = acc.balances.FirstOrDefault(b =>
                string.Equals(b.asset, asset, System.StringComparison.OrdinalIgnoreCase));

            if (balance == null) return 0m;

            return ToDec(balance.free);
        }

        public async Task<SellOrderResult> MarketSellEntireBalanceAsync(
            string symbol,
            SymbolInfo symbolInfo,
            CancellationToken ct)
        {
            // Extract base asset from symbol (e.g., "BTC" from "BTCUSDT")
            var baseAsset = symbol.EndsWith("USDT", System.StringComparison.OrdinalIgnoreCase)
                ? symbol[..^4]
                : symbol;

            // Get actual balance
            var actualBalance = await GetAssetBalanceAsync(baseAsset, ct);

            if (actualBalance <= 0)
            {
                return new SellOrderResult
                {
                    Symbol = symbol,
                    Success = false,
                    ErrorMessage = $"No {baseAsset} balance to sell"
                };
            }

            // Round DOWN to stepSize to maximize sellable amount
            var sellableQty = QuantityHelper.FloorToStepSize(actualBalance, symbolInfo.StepSize);
            var dustRemaining = actualBalance - sellableQty;

            if (sellableQty <= 0)
            {
                return new SellOrderResult
                {
                    Symbol = symbol,
                    Success = false,
                    DustRemaining = actualBalance,
                    ErrorMessage = $"Balance {actualBalance} rounds to zero with stepSize {symbolInfo.StepSize}"
                };
            }

            if (sellableQty < symbolInfo.MinQty)
            {
                return new SellOrderResult
                {
                    Symbol = symbol,
                    Success = false,
                    DustRemaining = actualBalance,
                    ErrorMessage = $"Sellable qty {sellableQty} is below minimum {symbolInfo.MinQty}"
                };
            }

            // Execute market sell
            var parameters = new Dictionary<string, string?>
            {
                ["symbol"] = symbol,
                ["side"] = "SELL",
                ["type"] = "MARKET",
                ["quantity"] = sellableQty.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            try
            {
                var response = await _signed.PostSignedAsync<OrderResponseDto>("/api/v3/order", parameters, ct);

                if (response == null)
                {
                    return new SellOrderResult
                    {
                        Symbol = symbol,
                        Success = false,
                        ErrorMessage = "Order response was null"
                    };
                }

                // Calculate average price from fills
                decimal totalQty = 0m;
                decimal totalValue = 0m;

                foreach (var fill in response.fills ?? new List<FillDto>())
                {
                    var fillQty = ToDec(fill.qty);
                    var fillPrice = ToDec(fill.price);
                    totalQty += fillQty;
                    totalValue += fillQty * fillPrice;
                }

                var avgPrice = totalQty > 0 ? totalValue / totalQty : 0m;

                return new SellOrderResult
                {
                    Symbol = symbol,
                    ExecutedQty = ToDec(response.executedQty),
                    AvgPrice = avgPrice,
                    DustRemaining = dustRemaining,
                    OrderId = response.orderId.ToString(),
                    Success = true
                };
            }
            catch (System.Exception ex)
            {
                return new SellOrderResult
                {
                    Symbol = symbol,
                    Success = false,
                    DustRemaining = actualBalance,
                    ErrorMessage = ex.Message
                };
            }
        }

        // ---- DTOs ----

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

        private sealed class OrderResponseDto
        {
            public string symbol { get; set; } = "";
            public long orderId { get; set; }
            public string executedQty { get; set; } = "0";
            public string status { get; set; } = "";
            public List<FillDto>? fills { get; set; }
        }

        private sealed class FillDto
        {
            public string price { get; set; } = "0";
            public string qty { get; set; } = "0";
            public string commission { get; set; } = "0";
            public string commissionAsset { get; set; } = "";
        }

        private sealed class OcoResponseDto
        {
            public long orderListId { get; set; }
            public string listClientOrderId { get; set; } = "";
            public string listOrderStatus { get; set; } = "";
        }

        private static decimal ToDec(string s) =>
            decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}