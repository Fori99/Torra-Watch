using System.Collections.Concurrent;

namespace torra_watch.Services
{
    /// <summary>
    /// Caches exchange info (LOT_SIZE, MIN_NOTIONAL, etc.) to avoid repeated API calls.
    /// Cache is refreshed every few hours.
    /// </summary>
    internal class ExchangeInfoCache
    {
        private readonly IMarketDataService _market;
        private readonly ConcurrentDictionary<string, CachedSymbolInfo> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _cacheTtl;

        public ExchangeInfoCache(IMarketDataService market, TimeSpan? cacheTtl = null)
        {
            _market = market;
            _cacheTtl = cacheTtl ?? TimeSpan.FromHours(4);
        }

        /// <summary>
        /// Gets symbol info from cache, fetching from API if needed.
        /// </summary>
        public async Task<SymbolInfo?> GetSymbolInfoAsync(string symbol, CancellationToken ct = default)
        {
            // Check if we have valid cached data
            if (_cache.TryGetValue(symbol, out var cached) && !cached.IsExpired)
            {
                return cached.Info;
            }

            // Fetch from API
            var info = await _market.GetExchangeInfoAsync(symbol, ct);

            if (info != null)
            {
                _cache[symbol] = new CachedSymbolInfo(info, DateTime.UtcNow.Add(_cacheTtl));
            }

            return info;
        }

        /// <summary>
        /// Forces a refresh of the cached symbol info.
        /// </summary>
        public async Task<SymbolInfo?> RefreshSymbolInfoAsync(string symbol, CancellationToken ct = default)
        {
            _cache.TryRemove(symbol, out _);
            return await GetSymbolInfoAsync(symbol, ct);
        }

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        private sealed class CachedSymbolInfo
        {
            public SymbolInfo Info { get; }
            public DateTime ExpiresAt { get; }
            public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

            public CachedSymbolInfo(SymbolInfo info, DateTime expiresAt)
            {
                Info = info;
                ExpiresAt = expiresAt;
            }
        }
    }
}
