using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI.Popups;

using Callisto.Controls;
using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.View;
using WB.SDK.Logging;

namespace WB.Craigslist8X.Model
{
    public static class Craigslist8XData
    {
        static Craigslist8XData()
        {
            _dataLock = new AsyncLock();
        }

        /// <summary>
        /// Initialize Craigslist8X data
        /// </summary>
        /// <returns>True if Craigslist8X data was loaded. False if data was already loaded.</returns>
        public static async Task<bool> LoadAsync()
        {
            if (Loaded)
            {
                return false; // Craigslist8X is already initialized
            }

            using (new MonitoredScope("Loading Craigslist8XData"))
            {
                using (await _dataLock.LockAsync())
                {
                    await CategoryManager.Instance.LoadAsync();
                    await CityManager.Instance.LoadAsync();
                    await RecentlyViewed.Instance.LoadAsync();
                    await FavoritePosts.Instance.LoadAsync();
                    await RecentlySearched.Instance.LoadAsync();
                    await SavedSearches.Instance.LoadAsync();
                    await UserAccounts.Instance.LoadAsync();
                }

                ++Settings.Instance.AppBootCount;

                Loaded = true;
            }

            return Loaded;
        }

        public static async Task SaveAsync()
        {
            using (new LoggerGroup("Saving Craigslist8XData"))
            {
                using (await _dataLock.LockAsync())
                {
                    await RecentlyViewed.Instance.SaveAsync();
                    await FavoritePosts.Instance.SaveAsync();
                    await RecentlySearched.Instance.SaveAsync();
                    await SavedSearches.Instance.SaveAsync();
                    await UserAccounts.Instance.SaveAsync();
                }
            }
        }

        #region Properties
        public static Geoposition Location
        {
            get;
            set;
        }

        public static bool Loaded
        {
            get;
            private set;
        }
        #endregion

        static AsyncLock _dataLock;
    }
}
