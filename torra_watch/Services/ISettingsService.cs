using torra_watch.UI.ViewModels;

namespace torra_watch.Services
{
    internal interface ISettingsService
    {
        StrategyConfigVM Load();
        void Save(StrategyConfigVM vm);
    }
}
