using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using torra_watch.Core;

namespace torra_watch.Exchange;

public sealed class BinanceHttpExchange //: IExchange, IDisposable
{
    private readonly HttpClient _http;
    private readonly ExchangeConfig _cfg;

    private readonly string _publicBaseUrl;
    private readonly string _privateBaseUrl;
    private readonly string _quote;

    public readonly record struct Bal(string Asset, decimal Free, decimal Locked);

    // -------- tradables cache(demo/testnet) --------
    private HashSet<string>? _tradablesCache;
    private DateTime _tradablesTs;
    private readonly TimeSpan _tradablesTtl = TimeSpan.FromMinutes(15);
    private bool _warnedTradablesOnce;

    //time sync for signed endpoints
    private long _serverTimeOffsetMs = 0;
    private const int RecvWindowMs = 5000;

    //----------------- ctor -----------------
    public BinanceHttpExchange(ExchangeConfig cfg, HttpClient http)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _http = http ?? throw new ArgumentNullException(nameof(http));

        _quote = (cfg.QuoteAsset ?? "USDT").ToUpperInvariant();

        if (_cfg.UseDemo)
        {
            _publicBaseUrl = "https://api.binance.com";
            _privateBaseUrl = "https://api.binance.com";
        }
        else if (_cfg.UseTestnet)
        {
            _publicBaseUrl = "https://testnet.binance.vision";
            _privateBaseUrl = "https://testnet.binance.vision";
        }
        else
        {
            _publicBaseUrl = "https://api.binance.com";
            _privateBaseUrl = "https://api.binance.com";
        }
    }

    public void Dispose() { /* HttpClient is owned by DI */ }

    //----------------- UI snapshot helpers -----------------
    public string PublicBaseUrl => _publicBaseUrl;
    public string PrivateBaseUrl => _privateBaseUrl;
    public string Quote => _quote;
    public bool KeysLoaded => !string.IsNullOrWhiteSpace(_cfg.ApiKey) && !string.IsNullOrWhiteSpace(_cfg.ApiSecret);

    public async Task<(string env, string publicHost, string privateHost, bool keysLoaded, List<Bal> balances)>
        GetEnvSnapshotAsync(CancellationToken ct = default)
    {
        string env = _cfg.UseDemo ? "DEMO" : _cfg.UseTestnet ? "TESTNET" : "LIVE";
        string pubHost = new Uri(_publicBaseUrl).Host;
        string privHost = new Uri(_privateBaseUrl).Host;

        var list = new List<Bal>();
        if (KeysLoaded)
        {
            try
            {
                var el = await SendSignedAsync<JsonElement>(HttpMethod.Get, "/api/v3/account", null, ct);
                if (el.TryGetProperty("balances", out var bals) && bals.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in bals.EnumerateArray())
                    {
                        var asset = b.TryGetProperty("asset", out var a) ? a.GetString() : null;
                        var free = ParseDec(b.TryGetProperty("free", out var fr) ? fr.GetString() : null);
                        var locked = ParseDec(b.TryGetProperty("locked", out var lk) ? lk.GetString() : null);
                        if (!string.IsNullOrWhiteSpace(asset) && (free != 0m || locked != 0m))
                            list.Add(new Bal(asset!, free, locked));
                    }
                }
            }
            catch { /* ignore; still show env/hosts/keys */ }
        }

        list = list.OrderByDescending(x => x.Free + x.Locked).Take(6).ToList();
        return (env, pubHost, privHost, KeysLoaded, list);
    }

    //----------------- Market data(public) -----------------
    private async Task<HashSet<string>?> TryGetTradablesAsync(CancellationToken ct)
    {
        if (_tradablesCache is not null && DateTime.UtcNow - _tradablesTs < _tradablesTtl)
            return _tradablesCache;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(10));
            var set = await GetTradableSymbolsOnPrivateBaseAsync(linked.Token);
            _tradablesCache = set;
            _tradablesTs = DateTime.UtcNow;
            return set;
        }
        catch when (!_warnedTradablesOnce)
        {
            _warnedTradablesOnce = true;
            return null; // continue without testnet filter
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<TickerInfo>> GetTopByQuoteVolumeAsync(int n = 150, CancellationToken ct = default)
    {
        var ticks = await GetPublicAsync<JsonElement>("/api/v3/ticker/24hr", null, ct);
        var books = await GetPublicAsync<JsonElement>("/api/v3/ticker/bookTicker", null, ct);

        var bookDict = new Dictionary<string, (decimal bid, decimal ask)>(StringComparer.OrdinalIgnoreCase);
        if (books.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in books.EnumerateArray())
            {
                var sy = x.TryGetProperty("symbol", out var ps) ? ps.GetString() : null;
                if (string.IsNullOrWhiteSpace(sy)) continue;
                var bid = ParseDec(x.TryGetProperty("bidPrice", out var b) ? b.GetString() : null);
                var ask = ParseDec(x.TryGetProperty("askPrice", out var a) ? a.GetString() : null);
                bookDict[sy] = (bid, ask);
            }
        }

        HashSet<string>? privTradables = null;
        if (_cfg.UseTestnet || _cfg.UseDemo)
            privTradables = await TryGetTradablesAsync(ct);

        var rows = new List<TickerInfo>(n);
        if (ticks.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in ticks.EnumerateArray())
            {
                var symbol = t.TryGetProperty("symbol", out var ps) ? ps.GetString() : null;
                if (string.IsNullOrWhiteSpace(symbol)) continue;

                if (!symbol.EndsWith(_quote, StringComparison.OrdinalIgnoreCase)) continue;
                if (symbol.Contains("BUSD", StringComparison.OrdinalIgnoreCase)) continue;
                if (symbol.Contains("FDUSD", StringComparison.OrdinalIgnoreCase)) continue;
                if (symbol.StartsWith(_quote, StringComparison.OrdinalIgnoreCase)) continue;

                if (privTradables is not null && !privTradables.Contains(symbol)) continue;

                var last = ParseDec(t.TryGetProperty("lastPrice", out var lp) ? lp.GetString() : null);
                var qvol = ParseDec(t.TryGetProperty("quoteVolume", out var qv) ? qv.GetString() : null);

                decimal spreadBps = 0m;
                if (bookDict.TryGetValue(symbol, out var bp) && bp.ask > 0 && bp.bid > 0)
                {
                    var mid = (bp.ask + bp.bid) / 2m;
                    if (mid > 0) spreadBps = (bp.ask - bp.bid) / mid * 10_000m;
                }

                rows.Add(new TickerInfo(symbol, last, qvol, spreadBps));
            }
        }

        return rows.OrderByDescending(r => r.QuoteVolume24h).Take(n).ToList();
    }

    public async Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct = default)
    {
        var el = await GetPublicAsync<JsonElement>("/api/v3/ticker/price", new() { ["symbol"] = symbol }, ct);
        return ParseDec(el.TryGetProperty("price", out var p) ? p.GetString() : null);
    }

    public async Task<decimal?> GetPriceAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        var start = ToMs(utc.AddMinutes(-2));
        var end = ToMs(utc.AddMinutes(1));

        var el = await GetPublicAsync<JsonElement>("/api/v3/klines", new()
        {
            ["symbol"] = symbol,
            ["interval"] = "1m",
            ["startTime"] = start.ToString(),
            ["endTime"] = end.ToString()
        }, ct);

        if (el.ValueKind != JsonValueKind.Array) return null;

        decimal? best = null;
        long bestDist = long.MaxValue;
        var targetMs = ToMs(utc);

        foreach (var k in el.EnumerateArray())
        {
            var close = ParseDec(k[4].GetString());
            var closeMs = k[6].GetInt64();
            var dist = Math.Abs(closeMs - targetMs);
            if (dist < bestDist) { bestDist = dist; best = close; }
        }
        return best;
    }

    public async Task<(decimal bid, decimal ask)> GetTopOfBookAsync(string symbol, CancellationToken ct = default)
    {
        var el = await GetPublicAsync<JsonElement>("/api/v3/ticker/bookTicker", new() { ["symbol"] = symbol }, ct);
        return (ParseDec(el.TryGetProperty("bidPrice", out var b) ? b.GetString() : null),
                ParseDec(el.TryGetProperty("askPrice", out var a) ? a.GetString() : null));
    }
    //----------------- Account & trading(signed) -----------------

    public async Task<decimal> GetEquityAsync(CancellationToken ct = default)
    {
        if (!KeysLoaded) return 0m;

        var el = await SendSignedAsync<JsonElement>(HttpMethod.Get, "/api/v3/account", null, ct);
        if (!el.TryGetProperty("balances", out var bals) || bals.ValueKind != JsonValueKind.Array) return 0m;

        foreach (var b in bals.EnumerateArray())
        {
            var asset = b.TryGetProperty("asset", out var a) ? a.GetString() : null;
            if (!string.IsNullOrWhiteSpace(asset) && asset.Equals(_quote, StringComparison.OrdinalIgnoreCase))
            {
                var free = ParseDec(b.TryGetProperty("free", out var fr) ? fr.GetString() : null);
                var locked = ParseDec(b.TryGetProperty("locked", out var lk) ? lk.GetString() : null);
                return free + locked;
            }
        }
        return 0m;
    }

    //IExchange: BASE-qty market buy
    public async Task<string> MarketBuyAsync(string symbol, decimal quantity, CancellationToken ct = default)
    {
        if (_cfg.ReadOnly) return "READONLY";

        var rules = await GetSymbolRulesDetailedAsync(symbol, ct);
        var qty = RoundDown(quantity, rules.StepSize);

        if (qty <= 0m)
            throw new InvalidOperationException($"Quantity rounds to zero. step={rules.StepSize}");

        var qp = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = "BUY",
            ["type"] = "MARKET",
            ["quantity"] = qty.ToString(CultureInfo.InvariantCulture)
        };

        var el = await SendSignedAsync<JsonElement>(HttpMethod.Post, "/api/v3/order", qp, ct);
        return el.TryGetProperty("orderId", out var id) ? id.GetInt64().ToString() : "OK";
    }

    //Convenience: QUOTE notional market buy(e.g., 50 USDT)
    public async Task<(string orderId, decimal executedQty)> MarketBuyWithQuoteAsync(
        string symbol, decimal quoteAmount, CancellationToken ct = default)
    {
        if (_cfg.ReadOnly) return ("READONLY", 0m);

        var rules = await GetSymbolRulesDetailedAsync(symbol, ct);
        if (quoteAmount < rules.MinNotional)
            throw new InvalidOperationException($"Notional too small: {quoteAmount} < {rules.MinNotional}");

        var qp = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = "BUY",
            ["type"] = "MARKET",
            ["quoteOrderQty"] = quoteAmount.ToString(CultureInfo.InvariantCulture)
        };

        var el = await SendSignedAsync<JsonElement>(HttpMethod.Post, "/api/v3/order", qp, ct);
        var id = el.TryGetProperty("orderId", out var oid) ? oid.GetInt64().ToString() : "OK";
        var executed = el.TryGetProperty("executedQty", out var eq) ? ParseDec(eq.GetString()) : 0m;
        return (id, executed);
    }

    //IExchange: Place OCO(uses tickSize for prices, stepSize for qty)
    public async Task PlaceOcoAsync(string symbol, decimal quantity, decimal takeProfitPrice, decimal stopLossPrice, CancellationToken ct = default)
        => await PlaceOcoReturnIdAsync(symbol, quantity, takeProfitPrice, stopLossPrice, ct);

    //Return orderListId so you can poll if desired
    public async Task<long> PlaceOcoReturnIdAsync(string symbol, decimal quantity, decimal takeProfitPrice, decimal stopLossPrice, CancellationToken ct = default)
    {
        if (_cfg.ReadOnly) return -1;

        var rules = await GetSymbolRulesDetailedAsync(symbol, ct);


        var qty = RoundDown(quantity, rules.StepSize);
        if (qty <= 0m) throw new InvalidOperationException($"Quantity rounds to zero. step={rules.StepSize}");

        var price = RoundDown(takeProfitPrice, rules.TickSize);
        var stopPrice = RoundDown(stopLossPrice, rules.TickSize);
        var stopLimit = RoundDown(stopPrice * 0.999m, rules.TickSize);

        //min notional(use TP price* qty as a conservative guard)
        var notional = price * qty;
        if (notional < rules.MinNotional)
            throw new InvalidOperationException($"Notional too small: {notional} < {rules.MinNotional}");

        var qp = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = "SELL",
            ["quantity"] = qty.ToString(CultureInfo.InvariantCulture),
            ["price"] = price.ToString(CultureInfo.InvariantCulture),
            ["stopPrice"] = stopPrice.ToString(CultureInfo.InvariantCulture),
            ["stopLimitPrice"] = stopLimit.ToString(CultureInfo.InvariantCulture),
            ["stopLimitTimeInForce"] = "GTC"
        };

        var el = await SendSignedAsync<JsonElement>(HttpMethod.Post, "/api/v3/order/oco", qp, ct);
        return el.TryGetProperty("orderListId", out var listId) ? listId.GetInt64() : -1;
    }

    public Task<bool> HasOpenPositionAsync(string symbol, CancellationToken ct = default)
        => Task.FromResult(false); // spot: we don’t model “positions”; we track our state

    public async Task ClosePositionAsync(string symbol, CancellationToken ct = default)
    {
        if (_cfg.ReadOnly) return;

        // 1) cancel all open orders for this symbol

        await SendSignedAsync<JsonElement>(HttpMethod.Delete, "/api/v3/openOrders", new() { ["symbol"] = symbol }, ct);

        // 2) sell any available base asset
        var baseAsset = symbol.EndsWith(_quote, StringComparison.OrdinalIgnoreCase) ? symbol[..^_quote.Length] : symbol;

        var acc = await SendSignedAsync<JsonElement>(HttpMethod.Get, "/api/v3/account", null, ct);
        if (!acc.TryGetProperty("balances", out var bals) || bals.ValueKind != JsonValueKind.Array) return;

        var freeBase = 0m;
        foreach (var b in bals.EnumerateArray())
        {
            var asset = b.TryGetProperty("asset", out var a) ? a.GetString() : null;
            if (!string.IsNullOrWhiteSpace(asset) && asset.Equals(baseAsset, StringComparison.OrdinalIgnoreCase))
            {
                freeBase = ParseDec(b.TryGetProperty("free", out var fr) ? fr.GetString() : null);
                break;
            }
        }

        if (freeBase > 0m)
        {
            var rules = await GetSymbolRulesDetailedAsync(symbol, ct);

            var qty = RoundDown(freeBase, rules.StepSize);
            if (qty > 0m)
            {
                await SendSignedAsync<JsonElement>(HttpMethod.Post, "/api/v3/order", new()
                {
                    ["symbol"] = symbol,
                    ["side"] = "SELL",
                    ["type"] = "MARKET",
                    ["quantity"] = qty.ToString(CultureInfo.InvariantCulture)
                }, ct);
            }
        }
    }

    // Rich method for internal use
    public sealed record SymbolRules(decimal StepSize, decimal MinQty, decimal TickSize, decimal MinNotional);

    public async Task<SymbolRules> GetSymbolRulesDetailedAsync(string symbol, CancellationToken ct = default)
    {
        var isSandbox = _cfg.UseTestnet || _cfg.UseDemo;
        var baseUrl = isSandbox ? _privateBaseUrl : _publicBaseUrl;

        var url = $"{baseUrl}/api/v3/exchangeInfo?symbol={Uri.EscapeDataString(symbol)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct);
        var txt = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(txt);
        var sym = doc.RootElement.GetProperty("symbols").EnumerateArray().First();

        decimal step = 0.000001m, minQty = 0m, tick = 0.000001m, minNotional = 10m;

        foreach (var f in sym.GetProperty("filters").EnumerateArray())
        {
            var type = f.GetProperty("filterType").GetString();
            if (type == "LOT_SIZE")
            {
                step = ParseDec(f.TryGetProperty("stepSize", out var s) ? s.GetString() : null);
                minQty = ParseDec(f.TryGetProperty("minQty", out var mq) ? mq.GetString() : null);
            }
            else if (type == "PRICE_FILTER")
            {
                tick = ParseDec(f.TryGetProperty("tickSize", out var t) ? t.GetString() : null);
            }
            else if (type == "MIN_NOTIONAL" || type == "NOTIONAL")
            {
                if (f.TryGetProperty("minNotional", out var mn))
                    minNotional = ParseDec(mn.GetString());
            }
        }
        return new SymbolRules(step, minQty, tick, minNotional);
    }

    // Explicit interface shape(for IExchange)
    public async Task<(decimal stepSize, decimal minNotional)> GetSymbolRulesAsync(
        string symbol, CancellationToken ct = default)
    {
        var r = await GetSymbolRulesDetailedAsync(symbol, ct);
        return (r.StepSize, r.MinNotional);
    }


    // ----------------- HTTP helpers -----------------
    private async Task<T> GetPublicAsync<T>(string path, Dictionary<string, string>? query, CancellationToken ct)
    {
        var url = BuildUrl(_publicBaseUrl, path, query);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct);
        var txt = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<T>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private async Task<T> SendSignedAsync<T>(HttpMethod method, string path, Dictionary<string, string>? query, CancellationToken ct)
    {
        EnsureHasKeys();
        if (_serverTimeOffsetMs == 0) await SyncTimeAsync(ct);

        string BuildSignedUrl(Dictionary<string, string> p)
        {
            p["timestamp"] = (ToMs(DateTime.UtcNow) + _serverTimeOffsetMs).ToString();
            p["recvWindow"] = RecvWindowMs.ToString();
            var qsLocal = ToQueryString(p);
            var sigLocal = Sign(qsLocal, _cfg.ApiSecret!);
            return $"{_privateBaseUrl}{path}?{qsLocal}&signature={sigLocal}";
        }

        var p0 = query is null ? new Dictionary<string, string>() : new Dictionary<string, string>(query);
        var url = BuildSignedUrl(p0);

        async Task<(bool ok, HttpResponseMessage resp, string body)> SendAsync(string u)
        {
            using var req = new HttpRequestMessage(method, u);
            req.Headers.Add("X-MBX-APIKEY", _cfg.ApiKey);
            var r = await _http.SendAsync(req, ct);
            var b = await r.Content.ReadAsStringAsync(ct);
            return (r.IsSuccessStatusCode, r, b);
        }

        var (ok, resp, body) = await SendAsync(url);
        if (!ok)
        {
            var (code, msg) = TryParseBinanceError(body);
            var timestampDrift = (code == -1021) ||
                                 (!string.IsNullOrEmpty(msg) && msg.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0);
            if (timestampDrift)
            {
                await SyncTimeAsync(ct);
                url = BuildSignedUrl(p0);
                (ok, resp, body) = await SendAsync(url);
            }
        }

        if (!ok) ThrowBinanceHttp(path, resp, body);
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static (int? code, string? msg) TryParseBinanceError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            int? code = doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null;
            string? msg = doc.RootElement.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return (code, msg);
        }
        catch { return (null, null); }
    }

    private static void ThrowBinanceHttp(string path, HttpResponseMessage resp, string body)
    {
        var (code, msg) = TryParseBinanceError(body);

        var shortBody = body;
        if (!string.IsNullOrEmpty(shortBody) && shortBody.Length > 600)
            shortBody = shortBody[..600] + " …";

        var baseMsg = $"Binance HTTP {(int)resp.StatusCode} {resp.StatusCode} at {path}"
                      + (code.HasValue ? $" | code={code}" : "")
                      + (!string.IsNullOrWhiteSpace(msg) ? $" | msg={msg}" : "");

        string hint = "";
        var m = (msg ?? "").ToLowerInvariant();
        if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            if (m.Contains("min_notional") || m.Contains("notional")) hint = " Hint: notional too small.";
            else if (m.Contains("lot_size")) hint = " Hint: qty step invalid — round to StepSize.";
            else if (m.Contains("price_filter")) hint = " Hint: price step invalid — round to TickSize.";
            else if (m.Contains("precision")) hint = " Hint: adjust decimals to filters.";
        }
        else if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 418)
        {
            hint = " Hint: rate limited — slow down.";
        }

        throw new InvalidOperationException($"{baseMsg}.{hint}\nBody: {shortBody}");
    }

    private async Task SyncTimeAsync(CancellationToken ct)
    {
        var el = await GetPublicAsync<JsonElement>("/api/v3/time", null, ct);
        var serverMs = el.TryGetProperty("serverTime", out var st) ? st.GetInt64() : ToMs(DateTime.UtcNow);
        _serverTimeOffsetMs = serverMs - ToMs(DateTime.UtcNow);
    }

    private static string BuildUrl(string baseUrl, string path, Dictionary<string, string>? query)
    {
        if (query is null || query.Count == 0) return $"{baseUrl}{path}";
        var qs = ToQueryString(query);
        return $"{baseUrl}{path}?{qs}";
    }

    private static string ToQueryString(Dictionary<string, string> p)
    {
        var sb = new StringBuilder();
        foreach (var kv in p.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value));
        }
        return sb.ToString();
    }

    private static string Sign(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static long ToMs(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    private static decimal ParseDec(string? s) => string.IsNullOrWhiteSpace(s) ? 0m : decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);

    private static decimal RoundDown(decimal value, decimal step) =>
        step <= 0 ? value : Math.Floor(value / step) * step;

    private void EnsureHasKeys()
    {
        if (string.IsNullOrWhiteSpace(_cfg.ApiKey) || string.IsNullOrWhiteSpace(_cfg.ApiSecret))
            throw new InvalidOperationException("Signed endpoint requires ApiKey/ApiSecret.");
    }

    private async Task<HashSet<string>> GetTradableSymbolsOnPrivateBaseAsync(CancellationToken ct)
    {
        var url = $"{_privateBaseUrl}/api/v3/exchangeInfo";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct);
        var txt = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(txt);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("symbols", out var syms) && syms.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in syms.EnumerateArray())
            {
                var name = s.TryGetProperty("symbol", out var n) ? n.GetString() : null;
                var status = s.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && string.Equals(status, "TRADING", StringComparison.OrdinalIgnoreCase))
                    set.Add(name!);
            }
        }
        return set;
    }

    //----------------- Debug/aux for UI -----------------
    public Task<string> DebugAccountRawAsync(CancellationToken ct = default)
        => SendSignedRawAsync(HttpMethod.Get, "/api/v3/account", null, ct);

    public Task<string> DebugOpenOrdersRawAsync(string symbol, CancellationToken ct = default)
        => SendSignedRawAsync(HttpMethod.Get, "/api/v3/openOrders", new() { ["symbol"] = symbol }, ct);

    public Task<string> DebugAllOrdersRawAsync(string symbol, CancellationToken ct = default)
        => SendSignedRawAsync(HttpMethod.Get, "/api/v3/allOrders", new() { ["symbol"] = symbol, ["limit"] = "50" }, ct);

    public async Task<string> SendSignedRawAsync(HttpMethod method, string path, Dictionary<string, string>? query, CancellationToken ct)
    {
        EnsureHasKeys();
        if (_serverTimeOffsetMs == 0) await SyncTimeAsync(ct);

        var p = query is null ? new Dictionary<string, string>() : new Dictionary<string, string>(query);
        p["timestamp"] = (ToMs(DateTime.UtcNow) + _serverTimeOffsetMs).ToString();
        p["recvWindow"] = RecvWindowMs.ToString();

        var qs = ToQueryString(p);
        var sig = Sign(qs, _cfg.ApiSecret!);
        var url = $"{_privateBaseUrl}{path}?{qs}&signature={sig}";

        using var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-MBX-APIKEY", _cfg.ApiKey);
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode && body.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            await SyncTimeAsync(ct);
            p["timestamp"] = (ToMs(DateTime.UtcNow) + _serverTimeOffsetMs).ToString();
            qs = ToQueryString(p);
            sig = Sign(qs, _cfg.ApiSecret!);
            url = $"{_privateBaseUrl}{path}?{qs}&signature={sig}";
            using var req2 = new HttpRequestMessage(method, url);
            req2.Headers.Add("X-MBX-APIKEY", _cfg.ApiKey);
            using var resp2 = await _http.SendAsync(req2, ct);
            var body2 = await resp2.Content.ReadAsStringAsync(ct);
            resp2.EnsureSuccessStatusCode();
            return body2;
        }

        resp.EnsureSuccessStatusCode();
        return body;
    }

    //Optional: get OCO list status for polling
    public async Task<JsonElement> GetOrderListAsync(long orderListId, CancellationToken ct = default)
        => await SendSignedAsync<JsonElement>(HttpMethod.Get, "/api/v3/orderList", new() { ["orderListId"] = orderListId.ToString() }, ct);

    //Account Details
    //public async Task<AccountSnapshotUsdt> GetAccountSnapshotUsdtAsync(CancellationToken ct = default)
    //{
    //    //1) balances(signed)
    //var acctJson = await SendSignedAsync(HttpMethod.Get, "/api/v3/account", null, ct);
    //    using var doc = JsonDocument.Parse(acctJson);

    //    var rawBalances = doc.RootElement.GetProperty("balances").EnumerateArray()
    //        .Select(b => new
    //        {
    //            asset = b.GetProperty("asset").GetString()!,
    //            free = decimal.Parse(b.GetProperty("free").GetString()!, CultureInfo.InvariantCulture),
    //            locked = decimal.Parse(b.GetProperty("locked").GetString()!, CultureInfo.InvariantCulture),
    //        })
    //        .Where(x => x.free + x.locked > 0m)
    //        .ToList();

    //    var usdtBal = rawBalances.FirstOrDefault(x => x.asset == "USDT");
    //    var mainUsdt = (usdtBal?.free ?? 0m) + (usdtBal?.locked ?? 0m);

    //    var nonUsdt = rawBalances.Where(x => x.asset != "USDT").ToList();
    //    if (nonUsdt.Count == 0)
    //        return new AccountSnapshotUsdt { Usdt = mainUsdt, Others = Array.Empty<AccountAssetUsdt>() };

    //    // 2) prices once (public)
    //    var pricesJson = await SendPublicAsync(HttpMethod.Get, "/api/v3/ticker/price", null, ct);
    //    var prices = JsonSerializer.Deserialize<List<TickerPrice>>(pricesJson, _j)!
    //                 .ToDictionary(p => p.symbol, p => p.price);

    //    decimal Px(string symbol) => prices.TryGetValue(symbol, out var v) ? v : 0m;

    //    var others = new List<AccountAssetUsdt>(nonUsdt.Count);
    //    foreach (var b in nonUsdt)
    //    {
    //        var total = b.free + b.locked;

    //        // Prefer direct {ASSET}USDT if it exists
    //        var px = Px($"{b.asset}USDT");
    //        var est = px > 0 ? total * px : 0m;

    //        others.Add(new AccountAssetUsdt
    //        {
    //            Asset = b.asset,
    //            Free = b.free,
    //            Locked = b.locked,
    //            EstUsdt = decimal.Round(est, 2)
    //        });
    //    }

    //    return new AccountSnapshotUsdt
    //    {
    //        Usdt = decimal.Round(mainUsdt, 2),
    //        Others = others
    //    };
    //}

    private sealed record TickerPrice(string symbol, decimal price);
}
