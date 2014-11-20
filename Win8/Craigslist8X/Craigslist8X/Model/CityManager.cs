using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.Storage.Streams;

using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK;
using WB.SDK.Logging;
using WB.SDK.Parsing;

namespace WB.Craigslist8X.Model
{
    internal class CityManager : BindableBase, IStorageBacked
    {
        #region Constructors
        static CityManager()
        {
            _instanceLock = new object();
        }

        private CityManager()
        {
            this._fileLock = new AsyncLock();
            this._searchCities = new ObservableCollection<CraigCity>();

            string xml = string.Format("<sc>{0}</sc>", Settings.Instance.SearchCities);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            IXmlNode root = doc.SelectSingleNode("/sc");
            if (!string.IsNullOrEmpty(root.InnerText))
            {
                // In 1.0 of Craigslist8X the data was not serialized in an XML format so it is possible that the 
                // text is just a single record.
                // Starting in 1.1 we contain each record in a "c" tag.

                XmlNodeList cities = doc.SelectNodes("/sc/c");
                if (cities.Count > 0)
                {
                    foreach (IXmlNode c in cities)
                    {
                        this._searchCities.Add(CraigCity.Deserialize(c.InnerText));
                    }
                }
                else
                {
                    this._searchCities.Add(CraigCity.Deserialize(doc.InnerText));
                }
            }
        }
        #endregion

        #region Singleton
        public static CityManager Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new CityManager();

                    return _instance;
                }
            }
        }

        static CityManager _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            using (new MonitoredScope("Load City Data"))
            {
                await Logger.Assert(!CitiesLoaded, "City data already loaded");

                bool ret = await LoadFromDisk();

                await Logger.Assert(ret, "Unable to load city data");
                return ret;
            }
        }

        /// <summary>
        /// Loads cities to local member variable
        /// </summary>
        /// <returns>Boolean indicating success</returns>
        private async Task<bool> LoadFromDisk()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.LocalFolder.GetFileAsync(CraigslistCitiesFileName);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Logger.LogException(ex);
            }

            if (file == null)
            {
                file = await SDK.Utilities.GetPackagedFile("Resources", "CraigslistCities.csv");
            }

            if (file != null)
            {
                CraigCityList cities = new CraigCityList();

                try
                {
                    string content = null;

                    using (await this._fileLock.LockAsync())
                    {
                        content = await file.LoadFileAsync();
                    }
                    
                    foreach (var line in content.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        CraigCity city = CraigCity.Deserialize(line.Trim());
                        cities.Add(city);
                    }

                    await Logger.Assert(cities.Count > 0, "should be more than 0 cities");
                    _cachedCities = cities;

                    List<CraigCity> sortedCities = cities.GetCities().ToList<CraigCity>();
                    sortedCities.Sort();

                    if (this.Cities == null)
                        this.Cities = new ObservableCollection<CraigCity>(sortedCities);
                    else
                        this.Cities.Copy(sortedCities);

                    this.Dirty = false;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debugger.Break();
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        /// <summary>
        /// Serializes the CraigCityList member field to disk.
        /// </summary>
        /// <returns>Boolean indicating success</returns>
        public async Task<bool> SaveAsync()
        {
            if (this._cachedCities == null)
            {
                await Logger.AssertNotReached("we have not cached cities! nothing to save");
                return false;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var city in _cachedCities.GetCities())
            {
                sb.AppendLine(CraigCity.Serialize(city));
            }

            using (await this._fileLock.LockAsync())
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(CraigslistCitiesFileName, CreationCollisionOption.ReplaceExisting);
                await file.SaveFileAsync(sb.ToString());
            }

            this.Dirty = false;
            return true;
        }

        private bool Dirty
        {
            get;
            set;
        }

        private AsyncLock _fileLock;
        #endregion
        
        #region Methods
        public CraigCity GetCityByUri(Uri uri)
        {
            return this._cachedCities.GetCityByUri(uri);
        }

        public void AddSearchCity(CraigCity city)
        {
            if (!this._searchCities.Contains(city))
            {
                this._searchCities.Add(city);

                this.SaveSearchCities();
            }
        }

        public void RemoveSearchCity(CraigCity city)
        {
            this._searchCities.Remove(city);

            this.SaveSearchCities();
        }

        public void PromoteSearchCity(CraigCity city)
        {
            if (this._searchCities.Contains(city))
            {
                int index = this._searchCities.IndexOf(city);
                this._searchCities.Move(index, 0);

                this.SaveSearchCities();
            }
        }

        public async Task<CraigCity> FindNearestCityGps(Geocoordinate coordinate)
        {
            if (this._cachedCities == null || this._cachedCities.Count == 0)
            {
                await Logger.AssertNotReached("Craig cities have not been cached!");
                return null;
            }

            CraigCity city = null;
            double diff = double.MaxValue;

            foreach (var c in this._cachedCities.GetCities())
            {
                if (c.Latitude == double.MinValue || c.Longitude == double.MinValue)
                    continue;

                double d = Math.Abs(coordinate.Latitude - c.Latitude) + Math.Abs(coordinate.Longitude - c.Longitude);

                if (d < diff)
                {
                    if (c != null)
                    {
                        city = c;
                        diff = d;
                    }
                }
            }

            return city;
        }
        #endregion

        #region Private Methods
        private void SaveSearchCities()
        {
            string xml = string.Empty;
            foreach (CraigCity c in this._searchCities)
            {
                xml += string.Format("<c>{0}</c>", CraigCity.Serialize(c));
            }
            Settings.Instance.SearchCities = xml;

            this.OnPropertyChanged("SearchCities");
        }
        #endregion

        #region Properties
        internal bool CitiesLoaded
        {
            get
            {
                return _cachedCities != null;
            }
        }

        internal bool SearchCitiesDefined
        {
            get
            {
                return this.SearchCities != null && this.SearchCities.Count > 0;
            }
        }

        internal ReadOnlyObservableCollection<CraigCity> SearchCities
        {
            get
            {
                return new ReadOnlyObservableCollection<CraigCity>(this._searchCities);
            }
        }

        internal ObservableCollection<CraigCity> Cities
        {
            get
            {
                return _cities;
            }
            private set
            {
                this.SetProperty(ref this._cities, value);
            }
        }
        #endregion

        #region Fields
        /// <summary>
        /// We need a CraigCityList because this is used by CraigslistApi methods
        /// </summary>
        CraigCityList _cachedCities;

        ObservableCollection<CraigCity> _cities;
        ObservableCollection<CraigCity> _searchCities;
        #endregion

        #region Constants
        internal const string CraigslistCitiesFileName = "CraigslistCities.csv";
        #endregion
    }
}
