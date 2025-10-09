using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using torra_watch.Core;
using torra_watch.Exchange;

namespace torra_watch;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // ---- Runtime config (edit here) ----
        var exCfg = new ExchangeConfig
        {
            UseBinance = true,       // true => use Binance HTTP adapter, false => PaperExchange
            ReadOnly = false,      // false => place orders; true => never place orders
            UseTestnet = false,      // testnet (testnet.binance.vision)
            UseDemo = true,       // demo (demo-api.binance.com)
            QuoteAsset = "USDT",
            ApiKey = Environment.GetEnvironmentVariable("TORRA_BINANCE_KEY"),
            ApiSecret = Environment.GetEnvironmentVariable("TORRA_BINANCE_SECRET")
        };

        // Safety: if orders are enabled but keys are missing, force ReadOnly.
        if (exCfg.UseBinance && !exCfg.ReadOnly &&
            (string.IsNullOrWhiteSpace(exCfg.ApiKey) || string.IsNullOrWhiteSpace(exCfg.ApiSecret)))
        {
            exCfg.ReadOnly = true;
        }

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Config
                services.AddSingleton(exCfg);

                // Shared HttpClient (gzip, sensible timeout)
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
                    return client;
                });

                // Exchange adapter
                if (exCfg.UseBinance)
                    services.AddSingleton<IExchange, BinanceHttpExchange>();
                else
                    services.AddSingleton<IExchange, PaperExchange>();

                // Core services
                services.AddSingleton<RankingService>();
                services.AddSingleton(new StrategyConfig
                {
                    UniverseSize = 150,
                    MinDrop3hPct = -0.04m,                       // <= -4% over last 3h
                    TakeProfitPct = 0.02m,                       // +2%
                    StopLossPct = 0.02m,                         // -2%
                    TimeStopHours = 6.0,                         // time-based exit
                    CooldownWhenNoCandidate = TimeSpan.FromHours(1)
                });
                services.AddSingleton<DecisionEngine>();
                services.AddSingleton<Trader>();

                // UI
                services.AddSingleton<MainForm>();
            })
            .Build();

        Application.Run(host.Services.GetRequiredService<MainForm>());
    }
}
