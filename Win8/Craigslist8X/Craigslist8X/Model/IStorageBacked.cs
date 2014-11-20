using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WB.Craigslist8X.Model
{
    interface IStorageBacked
    {
        Task<bool> LoadAsync();
        Task<bool> SaveAsync();
    }
}
