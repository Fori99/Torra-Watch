using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Headers;
using torra_watch.Core;

namespace torra_watch;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // ---- Load app settings (persisted JSON) ----
        var settings = BotSettings.LoadOrDefault();

        // ---- Build exchange config from settings + env-vars ----
        var exCfg = ExchangeFactory.Build(settings);

        // Safety: if trading is enabled but keys are missing, force ReadOnly.
        if (!settings.ReadOnly &&
            (string.IsNullOrWhiteSpace(exCfg.ApiKey) || string.IsNullOrWhiteSpace(exCfg.ApiSecret)))
        {
            settings.ReadOnly = true;
            // Persist the safeguard so UI reflects it.
            settings.Save();
        }

        // ---- Global unhandled exception hook (last-resort logging) ----
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            var msg = ex?.ToString() ?? "Unknown fatal error";
            //try { Log.Error($"UNHANDLED: {msg}"); } catch { /* ignore */ }
        };


        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Settings & configs
                services.AddSingleton(settings);
                services.AddSingleton(exCfg);

                // Shared HttpClient (gzip/deflate, sensible timeout, UA)
                services.AddSingleton<HttpClient>(_ =>
                {
                    var handler = new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    };

                    var client = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("TorraWatch/1.0 (+https://local.app)");
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                    return client;
                });

                // ---- Exchange adapter ----
                // If you add a PaperExchange later, branch here based on a setting.
                //services.AddSingleton<IExchange, BinanceHttpExchange>();

                // ---- Core services you already had ----
                services.AddSingleton<RankingService>();

                // Map StrategyConfig from BotSettings (pct → decimal)
                services.AddSingleton(new StrategyConfig
                {
                    UniverseSize = settings.TopN,
                    // Your engine expects a negative drop decimal (e.g., -0.04m for -4%):
                    MinDrop3hPct = -(settings.DropThresholdPct / 100m),
                    TakeProfitPct = (settings.TpPct / 100m),
                    StopLossPct = (settings.SlPct / 100m),
                    TimeStopHours = Math.Max(0.0, settings.MaxHoldingMinutes / 60.0),
                    CooldownWhenNoCandidate = TimeSpan.FromMinutes(settings.SymbolCooldownMin)
                });

                services.AddSingleton<DecisionEngine>();
                // services.AddSingleton<Trader>(); // enable when you add it

                // ---- UI ----
                services.AddSingleton<MainForm>();
            })
            .Build();

        // First log line (so we have a boot marker)
        //Log.Info($"Startup | mode={settings.Mode} readOnly={settings.ReadOnly} quote={settings.QuoteAsset}");

        Application.Run(host.Services.GetRequiredService<MainForm>());
    }
}
