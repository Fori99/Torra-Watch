//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using torra_watch.Exchange;

//namespace torra_watch.Core
//{
//    internal class EnvSnapshotService
//    {
//        public async Task<EnvSnapshot> GetAsync(BotSettings s, HttpClient http, CancellationToken ct = default)
//        {
//            var cfg = ExchangeFactory.Build(s);
//            using var ex = new BinanceHttpExchange(cfg, http);

//            var (env, pub, priv, keys, bals) = await ex.GetEnvSnapshotAsync(ct);
//            return new EnvSnapshot
//            {
//                Mode = s.Mode.ToString(),
//                PublicHost = pub,
//                PrivateHost = priv,
//                KeysLoaded = keys,
//                Balances = bals.Select(b => $"{b.Asset}:{b.Free + b.Locked:0.####}").ToArray()
//            };
//        }
//    }

//    public sealed class EnvSnapshot
//    {
//        public string Mode { get; set; } = "";
//        public string PublicHost { get; set; } = "";
//        public string PrivateHost { get; set; } = "";
//        public bool KeysLoaded { get; set; }
//        public string[] Balances { get; set; } = Array.Empty<string>();
//    }
//}
