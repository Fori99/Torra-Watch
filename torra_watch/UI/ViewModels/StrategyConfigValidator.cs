namespace torra_watch.UI.ViewModels
{
    internal class StrategyConfigValidator
    {
        public sealed record ValidationResult(bool Ok, string[] Errors)
        {
            public static ValidationResult Success() => new(true, Array.Empty<string>());
        }

        public static ValidationResult Validate(StrategyConfigVM vm)
        {
            var errs = new System.Collections.Generic.List<string>();

            if (vm.UniverseSize is < 20 or > 500)
                errs.Add("UniverseSize should be between 20 and 500.");
            if (vm.MinDrop3hPct is < 0.5m or > 25m)
                errs.Add("MinDrop3hPct should be between 0.5% and 25%.");
            if (vm.SecondWorstMinDropPct is < 0.5m or > 25m)
                errs.Add("SecondWorstMinDropPct should be between 0.5% and 25%.");
            if (vm.TakeProfitPct is < 0.2m or > 20m)
                errs.Add("TakeProfitPct should be between 0.2% and 20%.");
            if (vm.StopLossPct is < 0.2m or > 20m)
                errs.Add("StopLossPct should be between 0.2% and 20%.");
            if (vm.CooldownMinutes is < 1 or > 1440)
                errs.Add("CooldownMinutes should be between 1 and 1440.");

            // Optional sanity checks
            if (vm.TakeProfitPct <= vm.StopLossPct / 2m)
                errs.Add("TakeProfitPct looks very tight vs StopLossPct (TP <= SL/2).");
            if (vm.SecondWorstMinDropPct < vm.MinDrop3hPct)
                errs.Add("SecondWorstMinDropPct should be >= MinDrop3hPct.");

            return errs.Count == 0 ? ValidationResult.Success() : new(false, errs.ToArray());
        }
    }
}
