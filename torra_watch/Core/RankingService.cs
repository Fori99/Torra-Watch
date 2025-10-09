using System.Collections.Concurrent;

namespace torra_watch.Core
{
    public sealed class RankingService
    {
        private readonly IExchange _ex;
        public RankingService(IExchange ex) => _ex = ex;

        /// <summary>
        /// Build a ranking of the top N symbols by 24h quote volume, including 3h return.
        /// Robust to per-symbol failures and gentle on rate limits.
        /// </summary>
        public async Task<IReadOnlyList<RankingRow>> BuildAsync(int n = 150, CancellationToken ct = default)
        {
            // 1) universe: top by 24h quote volume
            var tickers = await _ex.GetTopByQuoteVolumeAsync(n, ct);

            // 2) fetch prices with bounded concurrency to avoid 429s
            var rows = new ConcurrentBag<RankingRow>();
            var sem = new SemaphoreSlim(12); // tweak parallelism if needed

            var tasks = tickers.Select(async t =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    // current price
                    var now = await _ex.GetLastPriceAsync(t.Symbol, ct);

                    // 3h-ago price (first try at exactly 3h)
                    var target = DateTime.UtcNow.AddHours(-3);
                    var ago = await _ex.GetPriceAtAsync(t.Symbol, target, ct);

                    // tiny fallback: if null, nudge 30s earlier in case of sparse candles
                    if (!ago.HasValue)
                        ago = await _ex.GetPriceAtAsync(t.Symbol, target.AddSeconds(-30), ct);

                    decimal? ret = null;
                    if (ago.HasValue && ago.Value > 0m && now > 0m)
                        ret = (now / ago.Value) - 1m;

                    rows.Add(new RankingRow(
                        Symbol: t.Symbol,
                        PriceNow: now,
                        Price3hAgo: ago,
                        Ret3h: ret,
                        QuoteVol24h: t.QuoteVolume24h));
                }
                catch
                {
                    // swallow per-symbol issues; add a null-return row so UI can still show it
                    rows.Add(new RankingRow(
                        Symbol: t.Symbol,
                        PriceNow: 0m,
                        Price3hAgo: null,
                        Ret3h: null,
                        QuoteVol24h: t.QuoteVolume24h));
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(tasks);

            // 3) sort: rows WITH Ret3h first (ascending), then the nulls (at the bottom)
            var sorted = rows
                .OrderBy(r => r.Ret3h.HasValue ? 0 : 1)
                .ThenBy(r => r.Ret3h ?? 0m)
                .ToList();

            return sorted;
        }
    }
}
