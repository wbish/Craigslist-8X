using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class SearchSettingsVM : INotifyPropertyChanged
    {
        public SearchSettingsVM()
        {
            CityManager.Instance.PropertyChanged += Craigslist8X_PropertyChanged;
        }

        void Craigslist8X_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SearchCities")
            {
                this.OnPropertyChanged("SearchCities");
                this.OnPropertyChanged("RemoveCityEnabled");
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        #region Properties
        public ReadOnlyObservableCollection<CraigCity> SearchCities
        {
            get
            {
                return CityManager.Instance.SearchCities;
            }
        }

        public string SearchCategory
        {
            get
            {
                return string.Format("{0} > {1}",
                    CategoryManager.Instance.SearchCategory.Root,
                    CategoryManager.Instance.SearchCategory.Name);
            }
        }

        public bool DetailedSearchResults
        {
            get
            {
                return Settings.Instance.DetailedSearchResults;
            }
            set
            {
                Settings.Instance.DetailedSearchResults = value;
            }
        }

        public bool SearchTitlesOnly
        {
            get
            {
                return Settings.Instance.OnlySearchTitles;
            }
            set
            {
                Settings.Instance.OnlySearchTitles = value;
            }
        }

        public bool PostsPicturesOnly
        {
            get
            {
                return Settings.Instance.OnlyShowPostsPictures;
            }
            set
            {
                Settings.Instance.OnlyShowPostsPictures = value;
            }
        }

        public bool RemoveCityEnabled
        {
            get
            {
                return CityManager.Instance.SearchCities != null && CityManager.Instance.SearchCities.Count > 1;
            }
        }
        #endregion
    }
}
