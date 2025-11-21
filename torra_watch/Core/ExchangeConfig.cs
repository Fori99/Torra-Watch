namespace torra_watch.Core
{
    public sealed class ExchangeConfig
    {
        public string? ApiKey { get; init; }
        public string? ApiSecret { get; init; }
        public string QuoteAsset { get; init; } = "USDT";
        public bool UseDemo { get; init; }
        public bool UseTestnet { get; init; }
        public bool ReadOnly { get; init; } = true;
    }

    public static class ExchangeFactory
    {
        // Env-var names per mode:
        // DEMO:    BINANCE_API_KEY_DEMO / BINANCE_API_SECRET_DEMO
        // TESTNET: BINANCE_API_KEY_TESTNET / BINANCE_API_SECRET_TESTNET
        // LIVE:    BINANCE_API_KEY_LIVE / BINANCE_API_SECRET_LIVE
        public static ExchangeConfig Build(BotSettings s)
        {
            string suffix = s.Mode switch
            {
                BotMode.DEMO => "DEMO",
                BotMode.TESTNET => "TESTNET",
                _ => "LIVE"
            };

            string? key = Environment.GetEnvironmentVariable($"BINANCE_API_KEY_{suffix}");
            string? sec = Environment.GetEnvironmentVariable($"BINANCE_API_SECRET_{suffix}");

            return new ExchangeConfig
            {
                ApiKey = key,
                ApiSecret = sec,
                QuoteAsset = s.QuoteAsset,
                UseDemo = s.Mode == BotMode.DEMO,
                UseTestnet = s.Mode == BotMode.TESTNET,
                ReadOnly = s.ReadOnly
            };
        }

    }
}
