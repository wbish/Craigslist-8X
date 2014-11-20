using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace WB.Craigslist8X.Model
{
    public class Settings
    {
        #region Initialization
        static Settings()
        {
            _instanceLock = new object();
        }

        private Settings()
        {
        }
        #endregion

        #region Singleton
        public static Settings Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new Settings();

                    return _instance;
                }
            }
        }

        static Settings _instance;
        static object _instanceLock;
        #endregion

        #region Properties
        public string SearchCities
        {
            get
            {
                return GetSetting(SearchCitiesKey, SearchCitiesDefault);
            }
            set
            {
                SetSetting(SearchCitiesKey, value);
            }
        }

        public string SearchCategory
        {
            get
            {
                return GetSetting(SearchCategoryKey, SearchCategoryDefault);
            }
            set
            {
                SetSetting(SearchCategoryKey, value);
            }
        }

        public int CitiesRefreshDays
        {
            get
            {
                return GetSetting(CitiesRefreshDaysKey, CitiesRefreshDaysDefault);
            }
            set
            {
                SetSetting(CitiesRefreshDaysKey, value);
            }
        }

        public int InAppPostCount
        {
            get
            {
                return GetSetting(InAppPostCountKey, InAppPostDefault);
            }
            set
            {
                SetSetting(InAppPostCountKey, value);
            }
        }

        public DateTime LastCitiesRefresh
        {
            get
            {
                return DateTime.Parse(GetSetting(LastCitiesRefreshKey, DateTime.MinValue.ToString()));
            }
            set
            {
                SetSetting(LastCitiesRefreshKey, value.ToString());
            }
        }

        public bool ShouldRefreshCities
        {
            get
            {
                TimeSpan span = DateTime.Now - LastCitiesRefresh;
                return span.TotalDays >= CitiesRefreshDays;
            }
        }

        public bool DetailedSearchResults
        {
            get
            {
                return GetSetting(DetailedSearchResultsKey, DetailedSearchResultsDefault);
            }
            set
            {
                SetSetting(DetailedSearchResultsKey, value);
            }
        }

        public bool OnlyShowPostsPictures
        {
            get
            {
                return GetSetting(OnlyShowPostsPicturesKey, OnlyShowPostsPicturesDefault);
            }
            set
            {
                SetSetting(OnlyShowPostsPicturesKey, value);
            }
        }

        public bool OnlySearchTitles
        {
            get
            {
                return GetSetting(OnlySearchTitleKey, OnlySearchTitlesDefault);
            }
            set
            {
                SetSetting(OnlySearchTitleKey, value);
            }
        }

        public bool TrackRecentlyViewedPosts
        {
            get
            {
                return GetSetting(TrackRecentlyViewedKey, TrackRecentlyViewedDefault);
            }
            set
            {
                SetSetting(TrackRecentlyViewedKey, value);
            }
        }

        public bool TrackRecentSearches
        {
            get
            {
                return GetSetting(TrackRecentSearchesKey, TrackRecentSearchesDefault);
            }
            set
            {
                SetSetting(TrackRecentSearchesKey, value);
            }
        }

        public bool ExpandPost
        {
            get
            {
                return GetSetting(ExpandPostKey, ExpandPostKeyDefault);
            }
            set
            {
                SetSetting(ExpandPostKey, value);
            }
        }

        public bool ExpandSearchResults
        {
            get
            {
                return GetSetting(ExpandSearchResultsKey, ExpandSearchResultsDefault);
            }
            set
            {
                SetSetting(ExpandSearchResultsKey, value);
            }
        }

        public bool CollapseOnSelection
        {
            get
            {
                return GetSetting(CollapseOnSelectionKey, CollapseOnSelectionDefault);
            }
            set
            {
                SetSetting(CollapseOnSelectionKey, value);
            }
        }

        public int AppBootCount
        {
            get
            {
                return GetSetting(AppBootCountKey, AppBootCountDefault);
            }
            set
            {
                SetSetting(AppBootCountKey, value);
            }
        }

        public bool ShowExpandTip
        {
            get
            {
                return GetSetting(ShowExpandTipKey, ShowExpandTipDefault);
            }
            set
            {
                SetSetting(ShowExpandTipKey, value);
            }
        }

        public bool PromptForRating
        {
            get
            {
                return GetSetting(PromptForRatingKey, PromptForRatingDefault);
            }
            set
            {
                SetSetting(PromptForRatingKey, value);
            }
        }

        public bool ShowPostNavBar
        {
            get
            {
                return GetSetting(ShowPostNavBarKey, ShowPostNavBarDefault);
            }
            set
            {
                SetSetting(ShowPostNavBarKey, value);
            }
        }

        public string LastReadNewsItem
        {
            get
            {
                return GetSetting(LastReadNewsItemKey, LastReadNewsItemDefault);
            }
            set
            {
                SetSetting(LastReadNewsItemKey, value);
            }
        }

        public bool PersonalsUnlocked
        {
            get
            {
                return GetSetting(PersonalsUnlockedKey, PersonalsUnlockDefault);
            }
            set
            {
                SetSetting(PersonalsUnlockedKey, value);
            }
        }
        #endregion

        #region Methods
        public static T GetSetting<T>(string key, T defaultValue)
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                ApplicationData.Current.LocalSettings.Values.Add(key, defaultValue);

            return (T)ApplicationData.Current.LocalSettings.Values[key];
        }

        public static void SetSetting<T>(string key, T value)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
            ApplicationData.Current.LocalSettings.Values.Add(key, value);
        }
        #endregion

        #region Constants
        private const string SearchCitiesKey = "CurrentCityKey";
        private const string SearchCitiesDefault = null;

        private const string SearchCategoryKey = "CurrentCategoryKey";
        private const string SearchCategoryDefault = "for sale,all for sale,sss";

        private const string CitiesRefreshDaysKey = "CitiesRefreshDays";
        private const int CitiesRefreshDaysDefault = 30;
        private const string LastCitiesRefreshKey = "DaysSinceCitiesRefresh";
        
        private const string CategoriesRefreshDaysKey = "CategoriesRefreshDays";
        private const int CategoriesRefreshDaysDefault = 30;
        private const string LastCategoriesRefreshKey = "DaysSinceCategoriesRefresh";

        private const string DetailedSearchResultsKey = "DetailedSearchResults";
        private const bool DetailedSearchResultsDefault = true;

        private const string OnlyShowPostsPicturesKey = "OnlyShowPostsPicturesKey";
        private const bool OnlyShowPostsPicturesDefault = false;

        private const string OnlySearchTitleKey = "OnlySearchTitlesKey";
        private const bool OnlySearchTitlesDefault = false;

        private const string TrackRecentlyViewedKey = "TrackRecentlyViewedKey";
        private const bool TrackRecentlyViewedDefault = true;

        private const string TrackRecentSearchesKey = "TrackRecentSearchesKey";
        private const bool TrackRecentSearchesDefault = true;

        private const string ExpandPostKey = "ExpandPostKey";
        private const bool ExpandPostKeyDefault = false;

        private const string ExpandSearchResultsKey = "ExpandSearchResultsKey";
        private const bool ExpandSearchResultsDefault = true;

        private const string CollapseOnSelectionKey = "CollapseOnSelectionKey";
        private const bool CollapseOnSelectionDefault = true;

        private const string AppBootCountKey = "AppBootCountKey";
        private const int AppBootCountDefault = 0;

        private const string ShowExpandTipKey = "ShowExpandTipKey";
        private const bool ShowExpandTipDefault = true;

        private const string PromptForRatingKey = "PromptForRatingKey";
        private const bool PromptForRatingDefault = true;

        private const string ShowPostNavBarKey = "ShowPostNavBarKey";
        private const bool ShowPostNavBarDefault = true;

        private const string InAppPostCountKey = "InAppPostCountKey";
        private const int InAppPostDefault = 0;

        private const string LastReadNewsItemKey = "LastReadNewsItemKey";
        private readonly string LastReadNewsItemDefault = DateTime.MinValue.ToString();

        private const string PersonalsUnlockedKey = "AppPUnlockedKey";
        private const bool PersonalsUnlockDefault = false;
        #endregion
    }
}
