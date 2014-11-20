using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

using WB.Craigslist8X.Common;

namespace WB.Craigslist8X.ViewModel
{
    public abstract class PostBase : BindableBase
    {
        public abstract PostType Type
        {
            get;
        }

        public enum PostType
        {
            Post,
            LoadMore,
            Ad,
        }
    }
}
