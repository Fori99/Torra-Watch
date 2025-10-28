//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using torra_watch.Core;
//using torra_watch.UI.ViewModels;

//namespace torra_watch.UI.Services
//{
//    public static class VmMapper
//    {
//        public static AccountVM ToVm(this AccountSnapshotUsdt s)
//        {
//            var others = s.Others
//                .OrderByDescending(a => a.EstUsdt)
//                .Select(a => new HoldingVM
//                {
//                    Asset = a.Asset,
//                    Free = a.Free,
//                    Locked = a.Locked,
//                    EstUsdt = a.EstUsdt
//                })
//                .ToList();

//            var total = s.Usdt + others.Sum(o => o.EstUsdt);
//            foreach (var o in others)
//            {
//                o.PercentOfTotal = total > 0 ? Math.Round((o.EstUsdt / total) * 100m, 2) : 0m;
//            }

//            return new AccountVM { MainUsdt = s.Usdt, OtherHoldings = others };
//        }
//    }
//}
