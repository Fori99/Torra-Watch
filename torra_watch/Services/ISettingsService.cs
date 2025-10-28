using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using torra_watch.UI.ViewModels;

namespace torra_watch.Services
{
    internal interface ISettingsService
    {
        StrategyConfigVM Load();
        void Save(StrategyConfigVM vm);
    }
}
