using torra_watch.UI.ViewModels;

namespace torra_watch.Services
{
    internal class JsonSettingsService : ISettingsService
    {
        private readonly string _path = Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
       "torra_watch", "settings.json");

        public StrategyConfigVM Load()
        {
            if (!File.Exists(_path)) return StrategyConfigVM.Defaults();
            var json = File.ReadAllText(_path);
            return System.Text.Json.JsonSerializer.Deserialize<StrategyConfigVM>(json)
                   ?? StrategyConfigVM.Defaults();
        }

        public void Save(StrategyConfigVM vm)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = System.Text.Json.JsonSerializer.Serialize(vm, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
    }
}
