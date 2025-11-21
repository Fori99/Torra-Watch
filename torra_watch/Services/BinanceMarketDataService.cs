using System.Net.Http.Json;
using System.Text.Json;
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
            var all = await _http.GetFromJsonAsync<List<TickerPrice>>("/api/v3/ticker/price", J, ct)
                      ?? new List<TickerPrice>();
            var set = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return all.Where(t => set.Contains(t.symbol))
                      .ToDictionary(t => t.symbol, t => t.price);
        }

        public async Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct)
        {
            try
            {
                var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
                var raw = await _http.GetFromJsonAsync<List<List<JsonElement>>>($"{url}", J, ct) ?? new();

                return raw.Select(r => new Candle
                {
                    OpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(r[0].GetInt64()).UtcDateTime,
                    Open = decimal.Parse(r[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    High = decimal.Parse(r[2].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Low = decimal.Parse(r[3].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Close = decimal.Parse(r[4].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Volume = decimal.Parse(r[5].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetKlinesAsync error for {symbol}: {ex.Message}");
                return new List<Candle>();
            }
        }

        public async Task<IReadOnlyList<TopMove>> GetTopMoversAsync(int universe, TimeSpan lookback, CancellationToken ct)
        {
            try
            {
                // Step 1: Get top coins by 24hr volume first (for filtering)
                var ticker24h = await _http.GetFromJsonAsync<List<Ticker24h>>("/api/v3/ticker/24hr", J, ct)
                                ?? new List<Ticker24h>();

                var topByVolume = ticker24h
                    .Where(t => t.symbol.EndsWith("USDT") && !string.IsNullOrEmpty(t.symbol))
                    .Where(t => t.lastPrice > 0 && t.quoteVolume > 0)
                    .OrderByDescending(t => t.quoteVolume)
                    .Take(universe)
                    .Select(t => t.symbol)
                    .ToList();

                Console.WriteLine($"Analyzing top {topByVolume.Count} coins by volume...");

                // Step 2: For each top coin, get price change over the actual lookback period
                var result = new List<TopMove>();

                // Calculate how many candles we need based on lookback
                // Use 5-minute candles for better performance
                var interval = "5m";
                var candlesNeeded = Math.Max(1, (int)Math.Ceiling(lookback.TotalMinutes / 5.0)) + 1;

                foreach (var symbol in topByVolume)
                {
                    try
                    {
                        // Get kline data
                        var klines = await GetKlinesAsync(symbol, interval, candlesNeeded, ct);

                        if (klines.Count < 2)
                        {
                            Console.WriteLine($"Skipping {symbol} - insufficient kline data");
                            continue;
                        }

                        var currentPrice = klines.Last().Close;
                        var priceAgo = klines.First().Close;

                        if (priceAgo == 0) continue;

                        var changePercent = ((currentPrice - priceAgo) / priceAgo) * 100m;

                        result.Add(new TopMove(
                            symbol.Replace("USDT", ""),
                            currentPrice,
                            priceAgo,
                            Math.Round(changePercent, 2)
                        ));

                        // Add small delay to avoid rate limits
                        await Task.Delay(50, ct);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {symbol}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Successfully analyzed {result.Count} coins");

                // Step 3: Sort from most LOSS to most PROFIT
                return result
                    .OrderBy(x => x.ChangePct)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetTopMoversAsync error: {ex.Message}");
                throw;
            }
        }

        public async Task<SymbolInfo?> GetExchangeInfoAsync(string symbol, CancellationToken ct)
        {
            try
            {
                var url = $"/api/v3/exchangeInfo?symbol={symbol}";
                var response = await _http.GetFromJsonAsync<ExchangeInfoResponse>(url, J, ct);

                if (response?.symbols == null || response.symbols.Count == 0)
                    return null;

                var symbolData = response.symbols[0];

                // Extract LOT_SIZE filter
                var lotSize = symbolData.filters?.FirstOrDefault(f => f.filterType == "LOT_SIZE");
                var minQty = lotSize?.minQty != null ? decimal.Parse(lotSize.minQty) : 0m;
                var maxQty = lotSize?.maxQty != null ? decimal.Parse(lotSize.maxQty) : 0m;
                var stepSize = lotSize?.stepSize != null ? decimal.Parse(lotSize.stepSize) : 0m;

                // Extract PRICE_FILTER
                var priceFilter = symbolData.filters?.FirstOrDefault(f => f.filterType == "PRICE_FILTER");
                var tickSize = priceFilter?.tickSize != null ? decimal.Parse(priceFilter.tickSize) : 0m;
                var minPrice = priceFilter?.minPrice != null ? decimal.Parse(priceFilter.minPrice) : 0m;
                var maxPrice = priceFilter?.maxPrice != null ? decimal.Parse(priceFilter.maxPrice) : 0m;

                // Extract MIN_NOTIONAL filter
                var minNotionalFilter = symbolData.filters?.FirstOrDefault(f => f.filterType == "MIN_NOTIONAL" || f.filterType == "NOTIONAL");
                var minNotional = minNotionalFilter?.minNotional != null ? decimal.Parse(minNotionalFilter.minNotional) : 0m;

                // Calculate precision from step/tick size
                int qtyPrecision = GetPrecisionFromStepSize(stepSize);
                int pricePrecision = GetPrecisionFromStepSize(tickSize);

                return new SymbolInfo
                {
                    Symbol = symbolData.symbol,
                    QuantityPrecision = qtyPrecision,
                    PricePrecision = pricePrecision,
                    MinQty = minQty,
                    MaxQty = maxQty,
                    StepSize = stepSize,
                    TickSize = tickSize,  // ⭐ ADD THIS
                    MinPrice = minPrice,  // ⭐ ADD THIS
                    MaxPrice = maxPrice,  // ⭐ ADD THIS
                    MinNotional = minNotional
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetExchangeInfoAsync error: {ex.Message}");
                return null;
            }
        }

        // Add this helper method
        private int GetPrecisionFromStepSize(decimal stepSize)
        {
            if (stepSize == 0) return 8;

            var stepStr = stepSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var decimalIndex = stepStr.IndexOf('.');

            if (decimalIndex == -1) return 0;

            // Count digits after decimal point until we hit a non-zero
            int precision = 0;
            for (int i = decimalIndex + 1; i < stepStr.Length; i++)
            {
                precision++;
                if (stepStr[i] != '0') break;
            }

            return precision;
        }

        // --- DTOs ---
        private sealed record TickerPrice(string symbol, decimal price);

        private sealed record Ticker24h(
            string symbol,
            decimal priceChange,
            decimal priceChangePercent,
            decimal lastPrice,
            decimal quoteVolume
        );

        private sealed class ExchangeInfoResponse
        {
            public List<SymbolData>? symbols { get; set; }
        }

        private sealed class SymbolData
        {
            public string symbol { get; set; } = "";
            public string status { get; set; } = "";
            public string baseAsset { get; set; } = "";
            public string quoteAsset { get; set; } = "";
            public int baseAssetPrecision { get; set; }
            public int quotePrecision { get; set; }
            public int quoteAssetPrecision { get; set; }
            public List<FilterData>? filters { get; set; }
        }

        private sealed class FilterData
        {
            public string filterType { get; set; } = "";
            public string? minPrice { get; set; }
            public string? maxPrice { get; set; }
            public string? tickSize { get; set; }
            public string? minQty { get; set; }
            public string? maxQty { get; set; }
            public string? stepSize { get; set; }
            public string? minNotional { get; set; }
        }
    }
}