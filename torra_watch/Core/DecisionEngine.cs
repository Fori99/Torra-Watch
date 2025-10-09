namespace torra_watch.Core
{
    public sealed class DecisionEngine
    {
        private readonly RankingService _ranking;
        private readonly StrategyConfig _cfg;

        public DecisionEngine(RankingService ranking, StrategyConfig cfg)
        {
            _ranking = ranking;
            _cfg = cfg;
        }

        public async Task<Decision> DecideAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // 1) Build universe
            var rows = await _ranking.BuildAsync(_cfg.UniverseSize, ct);
            if (rows == null || rows.Count == 0)
                return new(DecisionKind.Cooldown, now, null, null, now + _cfg.CooldownWhenNoCandidate,
                    "No symbols available.");

            // 2) Pick the worst (most negative) row that actually has a 3h return
            var top = rows.FirstOrDefault(r => r.Ret3h.HasValue);
            if (top is null)
                return new(DecisionKind.Cooldown, now, null, null, now + _cfg.CooldownWhenNoCandidate,
                    "No data rows with 3h return.");

            // 3) Entry rule: must be <= threshold (e.g., -4% => -0.04m)
            var ret = top.Ret3h!.Value;
            if (ret <= _cfg.MinDrop3hPct)
            {
                return new(
                    kind: DecisionKind.CandidateFound,
                    timeUtc: now,
                    symbol: top.Symbol,
                    ret3h: ret,
                    nextCheckUtc: null,
                    note: $"Candidate: {top.Symbol} (3h {(ret * 100m):0.00}%).");
            }

            // 4) Cooldown
            var cd = _cfg.CooldownWhenNoCandidate;
            return new(
                kind: DecisionKind.Cooldown,
                timeUtc: now,
                symbol: null,
                ret3h: null,
                nextCheckUtc: now + cd,
                note: $"No token ≤ {(_cfg.MinDrop3hPct * 100m):0.##}% 3h. Cooling down {cd.TotalMinutes:0.#} min.");
        }
    }
}
