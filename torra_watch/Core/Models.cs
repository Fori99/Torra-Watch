namespace torra_watch.Core;

public enum Signal { None, Enter, Exit }
public enum Side { Long, Flat }

public sealed record Candle(DateTime OpenTimeUtc, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

public sealed record TickerInfo(string Symbol, decimal LastPrice, decimal QuoteVolume24h, decimal SpreadBps);

public sealed record Quote3h(string Symbol, decimal PriceNow, decimal Price3hAgo, decimal Ret3h);

public sealed record Position(string Symbol, decimal EntryPrice, decimal Quantity, DateTime EntryTimeUtc);

public sealed record Trade(string Symbol, decimal EntryPrice, decimal ExitPrice, decimal Quantity, DateTime EntryTimeUtc, DateTime ExitTimeUtc, decimal FeesPctRoundTrip);

public sealed class AccountSnapshotUsdt
{
    public required decimal Usdt { get; init; }                    // USDT free + locked
    public required IReadOnlyList<AccountAssetUsdt> Others { get; init; } // non-USDT
}

public sealed class AccountAssetUsdt
{
    public required string Asset { get; init; }
    public decimal Free { get; init; }
    public decimal Locked { get; init; }
    public decimal EstUsdt { get; init; } // valued via {ASSET}USDT price if available, else 0
}