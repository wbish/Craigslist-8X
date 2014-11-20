using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class MainOptionsVM : BindableBase
    {
        public MainOptionsVM()
        {
            this.SetSearchSettings();
            CityManager.Instance.PropertyChanged += Model_PropertyChanged;
            CategoryManager.Instance.PropertyChanged += Model_PropertyChanged;
            SavedSearches.Instance.PropertyChanged += Model_PropertyChanged;
            this.SearchNotifications = SavedSearches.Instance.Notifications.ToString();
            this._showAds = !App.IsPro;
        }

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SearchCities")
            {
                this.SetSearchSettings();
            }
            else if (e.PropertyName == "SearchCategory")
            {
                this.SetSearchSettings();
            }
            else if (e.PropertyName == "Notifications")
            {
                this.SearchNotifications = SavedSearches.Instance.Notifications.ToString();
            }
        }

        private void SetSearchSettings()
        {
            this.SearchSettings = string.Format("{0} @ {1}", Utility.GetSearchCategoryLabel(), Utility.GetSearchCityLabel());
        }

        public string SearchSettings
        {
            get
            {
                return this._searchSettings;
            }
            set
            {
                this.SetProperty(ref this._searchSettings, value);
            }
        }

        public bool ShowAds
        {
            get
            {
                return this._showAds && !this.HideWebView;
            }
            set
            {
                this.SetProperty(ref this._showAds, value);
            }
        }

        public bool HideWebView
        {
            get
            { 
                return this._hideWebView; 
            }
            set
            {
                if (this.SetProperty(ref this._hideWebView, value))
                {
                    this.OnPropertyChanged("ShowAds");
                }
            }
        }

        public string SearchNotifications
        {
            get
            {
                return this._notifications == 0 ? string.Empty : this._notifications.ToString();
            }
            set
            {
                this.SetProperty(ref this._notifications, int.Parse(value));
            }
        }

        private bool _hideWebView;
        private bool _showAds;
        private string _searchSettings;
        private int _notifications;
    }
}
