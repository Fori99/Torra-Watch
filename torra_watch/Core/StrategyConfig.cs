namespace torra_watch.Core
{

    public sealed class StrategyConfig
    {
        public int UniverseSize { get; set; } = 150;
        public decimal MinDrop3hPct { get; set; } = -0.04m; // ≤ −4% to enter
        public decimal TakeProfitPct { get; set; } = 0.02m; // +2%
        public decimal StopLossPct { get; set; } = 0.02m; // -2%
        public double TimeStopHours { get; set; } = 6.0;   // close if neither TP/SL hit
        public TimeSpan CooldownWhenNoCandidate { get; set; } = TimeSpan.FromHours(1);

        public void Normalize()
        {
            UniverseSize = Math.Clamp(UniverseSize, 10, 500);
            // MinDrop3hPct should be negative (e.g., -0.04 = -4%)
            if (MinDrop3hPct > 0) MinDrop3hPct = -MinDrop3hPct / 100m; // if user typed "4", make it "-0.04"
            TakeProfitPct = Math.Clamp(TakeProfitPct, 0.001m, 0.10m);
            StopLossPct = Math.Clamp(StopLossPct, 0.001m, 0.10m);
            if (TimeStopHours < 0.25) TimeStopHours = 0.25; // min 15 minutes
            if (CooldownWhenNoCandidate <= TimeSpan.Zero) CooldownWhenNoCandidate = TimeSpan.FromMinutes(5);
        }

        public TimeSpan TimeStop => TimeSpan.FromHours(TimeStopHours);

        public override string ToString() =>
    $"N={UniverseSize}, Drop3h≤{MinDrop3hPct:P2}, TP {TakeProfitPct:P2}, SL {StopLossPct:P2}, TimeStop {TimeStopHours:0.#}h, Cooldown {CooldownWhenNoCandidate.TotalMinutes:0.#}m";

    }
}
