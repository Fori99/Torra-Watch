namespace torra_watch.Core;

public sealed record RankingRow(
    string Symbol,
    decimal PriceNow,
    decimal? Price3hAgo,
    decimal? Ret3h,          // (now / 3hAgo) - 1  → e.g., -0.042 = -4.2%
    decimal QuoteVol24h
);
