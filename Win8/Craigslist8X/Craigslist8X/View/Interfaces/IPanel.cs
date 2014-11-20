using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WB.Craigslist8X.View
{
    public interface IPanel
    {
        Task AttachContext(object context, IPanel parent);
    }
}
