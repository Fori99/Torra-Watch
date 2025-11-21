using System.Text.Json;
using System.Text.Json.Serialization;

namespace torra_watch.Core
{
    public enum BotMode { DEMO, TESTNET, LIVE }

    public class BotSettings
    {
        // --- Env / ops
        public BotMode Mode { get; set; } = BotMode.DEMO;
        public bool ReadOnly { get; set; } = true;
        public string QuoteAsset { get; set; } = "USDT";
        public string[] Blacklist { get; set; } = Array.Empty<string>();

        public int CooldownMinutes { get; set; } = 5;
        // --- Universe / filters
        public int TopN { get; set; } = 150;
        public decimal MinVol24h { get; set; } = 2_000_000m;
        public decimal MaxSpreadBps { get; set; } = 25m;

        // --- Signal
        public int LookbackMinutes { get; set; } = 180;
        public decimal DropThresholdPct { get; set; } = 4.0m;
        public int PickRank { get; set; } = 2;

        // --- Risk
        public decimal PositionPct { get; set; } = 10m;
        public decimal ReservePct { get; set; } = 10m;
        public int MaxConcurrent { get; set; } = 1;
        public int MaxTradesPerDay { get; set; } = 5;
        public decimal MaxDailyLossPct { get; set; } = 3m;

        // --- Exits
        public decimal TpPct { get; set; } = 2.0m;
        public decimal SlPct { get; set; } = 1.0m;
        public int MaxHoldingMinutes { get; set; } = 360;
        public bool EnableBreakeven { get; set; } = false;
        public bool EnableTrailing { get; set; } = false;

        // --- Timers
        public int ScanIntervalMin { get; set; } = 1;
        public int MonitorIntervalMin { get; set; } = 1;
        public int SymbolCooldownMin { get; set; } = 90;
        public decimal SlippageCapPct { get; set; } = 0.5m;

        // --- File IO helpers
        [JsonIgnore]
        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TorraWatch", "settings.json");

        public static BotSettings LoadOrDefault()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<BotSettings>(json, JsonOptions()) ?? new();
            }
            catch { return new(); }
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions(withIndent: true)));
        }

        public static JsonSerializerOptions JsonOptions(bool withIndent = false) => new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = withIndent,
            AllowTrailingCommas = true
        };
    }
}
