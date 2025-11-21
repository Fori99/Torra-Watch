using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using torra_watch.UI.Controls;
using torra_watch.UI.ViewModels;

namespace torra_watch.Services
{
    public class BinanceSignedClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _mode;
        private readonly SystemLogsControl? _logger;
        private readonly string _baseUrl;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // Updated BinanceSignedClient with api.binance.com support

        public BinanceSignedClient(HttpClient http, string apiKey, string apiSecret, string mode, SystemLogsControl? logger = null)
        {
            _http = http;
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _mode = mode.ToLowerInvariant();
            _logger = logger;

            // CRITICAL UPDATE: Demo has its own API endpoint!
            _baseUrl = _mode switch
            {
                "demo" => "https://api.binance.com",      // <-- Demo API endpoint exists!
                "testnet" => "https://testnet.binance.vision",  // Testnet endpoint
                "live" => "https://api.binance.com",            // Live trading
                "production" => "https://api.binance.com",      // Same as live
                _ => "https://api.binance.com"                  // Default to live
            };

            _http.BaseAddress = new Uri(_baseUrl);
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);

            _logger?.Append($"Binance client initialized - Mode: {_mode}, URL: {_baseUrl}", LogLevel.Info);

            if (_mode == "demo")
            {
                _logger?.Append("Using DEMO mode with api.binance.com", LogLevel.Info);
                _logger?.Append("Trades will appear on demo.binance.com web interface", LogLevel.Info);
            }
        }

        /// <summary>
        /// Generic version that deserializes the response to type T
        /// </summary>
        public async Task<T?> GetSignedAsync<T>(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct = default) where T : class
        {
            var jsonResponse = await GetSignedAsync(endpoint, parameters, ct);

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger?.Append($"Empty response for {endpoint}", LogLevel.Warning);
                return null;
            }

            try
            {
                var result = JsonSerializer.Deserialize<T>(jsonResponse, JsonOptions);
                return result;
            }
            catch (JsonException ex)
            {
                _logger?.Append($"JSON deserialization error for {endpoint}: {ex.Message}", LogLevel.Error);
                _logger?.Append($"Raw response: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}", LogLevel.Debug);
                throw;
            }
        }

        /// <summary>
        /// Non-generic version that returns raw JSON string
        /// </summary>
        public async Task<string> GetSignedAsync(string endpoint, CancellationToken ct)
        {
            return await GetSignedAsync(endpoint, new Dictionary<string, string?>(), ct);
        }

        /// <summary>
        /// Main implementation that handles signing and making the request
        /// </summary>
        public async Task<string> GetSignedAsync(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct)
        {
            try
            {
                // Build query string from parameters
                var queryParams = new List<string>();
                foreach (var param in parameters.Where(p => p.Value != null))
                {
                    queryParams.Add($"{param.Key}={Uri.EscapeDataString(param.Value!)}");
                }

                // Add timestamp
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                queryParams.Add($"timestamp={timestamp}");

                var queryString = string.Join("&", queryParams);

                // Generate signature
                var signature = GenerateSignature(queryString);
                queryString = $"{queryString}&signature={signature}";

                // Build full URL
                var fullUrl = endpoint.Contains("?")
                    ? $"{endpoint}&{queryString}"
                    : $"{endpoint}?{queryString}";

                _logger?.Append($"Request: {_baseUrl}{endpoint}", LogLevel.Debug);

                // Make the request
                var response = await _http.GetAsync(fullUrl, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.Append($"API Error {response.StatusCode}: {content}", LogLevel.Error);

                    // Parse error for better reporting
                    try
                    {
                        var errorJson = JsonDocument.Parse(content);
                        if (errorJson.RootElement.TryGetProperty("code", out var code) &&
                            errorJson.RootElement.TryGetProperty("msg", out var msg))
                        {
                            var errorCode = code.GetInt32();
                            var errorMsg = msg.GetString();

                            // Provide specific guidance
                            switch (errorCode)
                            {
                                case -2015:
                                    _logger?.Append("Invalid API-key, IP, or permissions. Check:", LogLevel.Error);
                                    _logger?.Append("1. API key matches the endpoint (testnet/live/demo)", LogLevel.Error);
                                    _logger?.Append("2. IP whitelist settings in Binance API Management", LogLevel.Error);
                                    _logger?.Append("3. API key has required permissions enabled", LogLevel.Error);
                                    break;
                                case -1021:
                                    _logger?.Append("Timestamp error - sync your system clock", LogLevel.Error);
                                    break;
                                case -1022:
                                    _logger?.Append("Invalid signature - check your API secret", LogLevel.Error);
                                    break;
                            }

                            throw new Exception($"Binance API error {errorCode}: {errorMsg}");
                        }
                    }
                    catch (JsonException)
                    {
                        // If we can't parse the error, throw with the raw content
                    }

                    throw new Exception($"Binance API error: {content}");
                }

                return content;
            }
            catch (HttpRequestException ex)
            {
                _logger?.Append($"Network error: {ex.Message}", LogLevel.Error);
                throw;
            }
            catch (TaskCanceledException)
            {
                _logger?.Append("Request timeout or cancelled", LogLevel.Warning);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Append($"GetSignedAsync error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        private string GenerateSignature(string queryString)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        /// <summary>
        /// POST request with signature (for placing orders, etc.)
        /// </summary>
        public async Task<T?> PostSignedAsync<T>(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct = default) where T : class
        {
            var jsonResponse = await PostSignedAsync(endpoint, parameters, ct);

            if (string.IsNullOrWhiteSpace(jsonResponse))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(jsonResponse, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger?.Append($"JSON deserialization error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// POST request that returns raw JSON string
        /// </summary>
        public async Task<string> PostSignedAsync(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct)
        {
            try
            {
                // Build form data with parameters
                var formData = new List<KeyValuePair<string, string>>();
                foreach (var param in parameters.Where(p => p.Value != null))
                {
                    formData.Add(new KeyValuePair<string, string>(param.Key, param.Value!));
                }

                // Add timestamp
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                formData.Add(new KeyValuePair<string, string>("timestamp", timestamp.ToString()));

                // Generate signature
                var queryString = string.Join("&", formData.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                var signature = GenerateSignature(queryString);
                formData.Add(new KeyValuePair<string, string>("signature", signature));

                // Create form content
                var content = new FormUrlEncodedContent(formData);

                _logger?.Append($"POST Request: {_baseUrl}{endpoint}", LogLevel.Debug);

                // Make the request
                var response = await _http.PostAsync(endpoint, content, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.Append($"API Error {response.StatusCode}: {responseContent}", LogLevel.Error);
                    throw new Exception($"Binance API error: {responseContent}");
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                _logger?.Append($"PostSignedAsync error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        /// <summary>
        /// DELETE request with signature (for canceling orders)
        /// </summary>
        public async Task<T?> DeleteSignedAsync<T>(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct = default) where T : class
        {
            var jsonResponse = await DeleteSignedAsync(endpoint, parameters, ct);

            if (string.IsNullOrWhiteSpace(jsonResponse))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(jsonResponse, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger?.Append($"JSON deserialization error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        public async Task<string> DeleteSignedAsync(string endpoint, Dictionary<string, string?> parameters, CancellationToken ct)
        {
            try
            {
                // Build query string
                var queryParams = new List<string>();
                foreach (var param in parameters.Where(p => p.Value != null))
                {
                    queryParams.Add($"{param.Key}={Uri.EscapeDataString(param.Value!)}");
                }

                // Add timestamp
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                queryParams.Add($"timestamp={timestamp}");

                var queryString = string.Join("&", queryParams);

                // Generate signature
                var signature = GenerateSignature(queryString);
                queryString = $"{queryString}&signature={signature}";

                // Build full URL
                var fullUrl = endpoint.Contains("?")
                    ? $"{endpoint}&{queryString}"
                    : $"{endpoint}?{queryString}";

                _logger?.Append($"DELETE Request: {_baseUrl}{endpoint}", LogLevel.Debug);

                // Make the request
                var response = await _http.DeleteAsync(fullUrl, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.Append($"API Error {response.StatusCode}: {content}", LogLevel.Error);
                    throw new Exception($"Binance API error: {content}");
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger?.Append($"DeleteSignedAsync error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}