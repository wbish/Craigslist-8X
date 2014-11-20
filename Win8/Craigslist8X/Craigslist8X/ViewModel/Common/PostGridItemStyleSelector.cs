using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class PostGridItemStyleSelector : StyleSelector
    {
        public Style SearchItemPostFull
        {
            get;
            set;
        }

        public Style SearchItemPostSimple
        {
            get;
            set;
        }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (Settings.Instance.DetailedSearchResults)
            {
                return SearchItemPostFull;
            }
            else
            {
                return SearchItemPostSimple;
            }
        }
    }
}
