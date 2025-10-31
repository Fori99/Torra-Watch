using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using torra_watch.UI.Controls;
using torra_watch.UI.ViewModels;

namespace torra_watch.Services;

public sealed class BinanceSignedClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _secret;
    private long _serverTimeOffsetMs = 0; // serverTime - localUtcMs
    private readonly string _apiSecret;
    private readonly string _baseUrl;
    private long _timeOffset = 0;

    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    private readonly SystemLogsControl? _logger;

    public BinanceSignedClient(HttpClient http, string apiKey, string apiSecret, string mode = "live", SystemLogsControl? logger = null)
    {
        _http = http;
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _logger = logger;

        var baseUrl = mode.ToLowerInvariant() switch
        {
            "demo" => "https://testnet.binance.vision",
            "testnet" => "https://testnet.binance.vision",
            _ => "https://api.binance.com"
        };

        _http.BaseAddress = new Uri(baseUrl);
        _http.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);

        // Sync time with server
        _ = SyncTimeAsync();

        _logger?.Append($"BinanceSignedClient initialized: {baseUrl}", LogLevel.Debug);
    }

    private async Task SyncTimeAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/v3/time");
            var json = await response.Content.ReadAsStringAsync();
            var serverTime = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("serverTime").GetInt64();
            var localTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _timeOffset = serverTime - localTime;

            _logger?.Append($"Time synced. Offset: {_timeOffset}ms", LogLevel.Debug);
        }
        catch (Exception ex)
        {
            _logger?.Append($"Time sync failed: {ex.Message}", LogLevel.Warning);
        }
    }

    public async Task<string> GetAsync(string endpoint, Dictionary<string, string>? parameters = null, CancellationToken ct = default)
    {
        try
        {
            var queryString = BuildQueryString(parameters);
            var signature = Sign(queryString);
            var url = $"{endpoint}?{queryString}&signature={signature}";

            _logger?.Append($"→ Request: GET {endpoint}", LogLevel.Debug);
            _logger?.Append($"→ Full URL: {_http.BaseAddress}{url}", LogLevel.Debug);

            var response = await _http.GetAsync(url, ct);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.Append($"✗ HTTP {(int)response.StatusCode}: {responseBody}", LogLevel.Error);
                throw new HttpRequestException($"Binance API error: {responseBody}");
            }

            _logger?.Append($"✓ Response received ({responseBody.Length} chars)", LogLevel.Debug);
            return responseBody;
        }
        catch (Exception ex)
        {
            _logger?.Append($"✗ Request exception: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    private string BuildQueryString(Dictionary<string, string>? parameters)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (parameters == null || parameters.Count == 0)
            return $"recvWindow=10000&timestamp={timestamp}";

        var query = string.Join("&", parameters.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{query}&recvWindow=10000&timestamp={timestamp}";
    }

    private string Sign(string queryString)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_apiSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(queryString));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    public async Task EnsureTimeSyncAsync(CancellationToken ct)
    {
        var resp = await _http.GetAsync("/api/v3/time", ct);
        resp.EnsureSuccessStatusCode();
        var obj = await resp.Content.ReadFromJsonAsync<ServerTimeDto>(J, ct);
        var server = obj?.serverTime ?? 0;
        var local = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _serverTimeOffsetMs = server - local;
    }

    

    private long ServerTimestampMs() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverTimeOffsetMs;

    public async Task<T?> GetSignedAsync<T>(string path, Dictionary<string, string?> q, CancellationToken ct)
    {
        if (_serverTimeOffsetMs == 0) await EnsureTimeSyncAsync(ct);

        q["timestamp"] = ServerTimestampMs().ToString();
        q["recvWindow"] = "5000";

        var query = string.Join("&", q.Where(kv => !string.IsNullOrEmpty(kv.Value))
                                      .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
        var sig = Sign(query);
        var url = $"{path}?{query}&signature={sig}";

        using var resp = await _http.GetAsync(url, ct);
        // handle timestamp drift retry
        if ((int)resp.StatusCode == 400)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (body.Contains("-1021")) // timestamp for this request is outside of recvWindow
            {
                await EnsureTimeSyncAsync(ct);
                return await GetSignedAsync<T>(path, q, ct); // one retry after re-sync
            }
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(J, ct);
    }

    private sealed record ServerTimeDto(long serverTime);
}
