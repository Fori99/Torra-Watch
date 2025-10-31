using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using torra_watch.Core;
using torra_watch.Models;


namespace torra_watch.Services
{
    public interface IAccountService
    {
        Task<AccountSnapshot> GetBalancesAsync(CancellationToken ct);
        Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsync(CancellationToken ct);
    }

}
