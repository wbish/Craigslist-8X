using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WB.Craigslist8X.ViewModel
{
    public class PostAd : PostBase
    {
        public override PostType Type
        {
            get { return PostType.Ad; }
        }
    }
}
