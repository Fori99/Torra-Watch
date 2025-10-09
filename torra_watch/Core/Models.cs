namespace torra_watch.Core;

public enum Signal { None, Enter, Exit }
public enum Side { Long, Flat }

public sealed record Candle(DateTime OpenTimeUtc, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

public sealed record TickerInfo(string Symbol, decimal LastPrice, decimal QuoteVolume24h, decimal SpreadBps);

public sealed record Quote3h(string Symbol, decimal PriceNow, decimal Price3hAgo, decimal Ret3h);

public sealed record Position(string Symbol, decimal EntryPrice, decimal Quantity, DateTime EntryTimeUtc);

public sealed record Trade(string Symbol, decimal EntryPrice, decimal ExitPrice, decimal Quantity,
    DateTime EntryTimeUtc, DateTime ExitTimeUtc, decimal FeesPctRoundTrip);
