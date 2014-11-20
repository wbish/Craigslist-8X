using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class GeneralSettingsVM : BindableBase
    {
        public bool TrackRecentlyViewed
        {
            get
            {
                return Settings.Instance.TrackRecentlyViewedPosts;
            }
            set
            {
                Settings.Instance.TrackRecentlyViewedPosts = value;
            }
        }

        public bool TrackRecentlySearched
        {
            get
            {
                return Settings.Instance.TrackRecentSearches;
            }
            set
            {
                Settings.Instance.TrackRecentSearches = value;
            }
        }
    }
}
