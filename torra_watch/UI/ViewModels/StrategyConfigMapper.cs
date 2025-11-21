using torra_watch.Core;

namespace torra_watch.UI.ViewModels
{
    internal class StrategyConfigMapper
    {
        // Map persisted core settings -> your VM (used to populate the panel)
        public static StrategyConfigVM FromSettings(BotSettings s) => new()
        {
            UniverseSize = s.TopN,
            MinDrop3hPct = s.DropThresholdPct,          // stored as percent in BotSettings
            TakeProfitPct = s.TpPct,                    // percent
            StopLossPct = s.SlPct,                      // percent
            CooldownMinutes = s.SymbolCooldownMin,
            SecondWorstMinDropPct = s.DropThresholdPct  // mirror unless you add a separate field
        };

        // Apply edited VM -> core settings (then persist with s.Save())
        public static void ApplyToSettings(StrategyConfigVM vm, BotSettings s)
        {
            s.TopN = vm.UniverseSize;
            s.DropThresholdPct = vm.MinDrop3hPct;
            s.TpPct = vm.TakeProfitPct;
            s.SlPct = vm.StopLossPct;
            s.SymbolCooldownMin = vm.CooldownMinutes;
            // If later you want a distinct “second worst” threshold, add it to BotSettings and map here.
        }
    }
}
