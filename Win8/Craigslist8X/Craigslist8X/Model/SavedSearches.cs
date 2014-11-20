using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Notifications;


using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.Model
{
    public class SavedSearches : BindableBase, IStorageBacked
    {
        #region Initialization
        static SavedSearches()
        {
            _instanceLock = new object();
        }

        private SavedSearches()
        {
            this.Queries = new ObservableCollection<SavedQuery>();
            this._listLock = new object();
            this._fileLock = new AsyncLock();
            this._notificationFileLock = new AsyncLock();
        }
        #endregion

        #region Singleton
        public static SavedSearches Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new SavedSearches();

                    return _instance;
                }
            }
        }

        static SavedSearches _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.RoamingFolder.GetFileAsync(SavedSearchesFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }

            if (file != null)
            {
                Logger.LogMessage("SavedSearches", "Loading SavedSearches.xml");

                try
                {
                    string content = null;

                    using (await this._fileLock.LockAsync())
                    {
                        content = await file.LoadFileAsync();
                    }

                    if (string.IsNullOrEmpty(content))
                        return false;

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(content);

                    XmlNodeList queries = doc.SelectNodes("/root/sq");

                    lock (this._listLock)
                    {
                        this.Queries.Clear();

                        foreach (var qn in queries)
                        {
                            SavedQuery sq = SavedQuery.Deserialize(qn.GetXml());
                            sq.PropertyChanged += SavedQuery_PropertyChanged;
                            this.Queries.Add(sq);
                        }
                    }

                    this.Dirty = false;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debugger.Break();
                    Logger.LogMessage("SavedSearches", "Failed to read SavedSearches.xml");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveAsync()
        {
            Logger.LogMessage("SavedSearches", "Saving SavedSearches.xml");

            if (!this.Dirty)
            {
                Logger.LogMessage("SavedSearches", "No changes to persist to SavedSearches.xml");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("<root>");
                lock (_listLock)
                {
                    for (int i = 0; i < _queries.Count; ++i)
                    {
                        sb.AppendLine(SavedQuery.Serialize(_queries[i]));
                    }
                }
                sb.Append("</root>");

                using (await this._fileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.RoamingFolder.CreateFileAsync(SavedSearchesFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                this.Dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("SavedSearches", "Failed to save SavedSearches.xml");
                Logger.LogException(ex);
            }

            return false;
        }

        private bool Dirty
        {
            get;
            set;
        }

        private AsyncLock _fileLock;
        #endregion

        #region Notification Data
        public async Task<bool> LoadNotificationsAsync()
        {
            if (this._queries == null || this._queries.Count == 0)
                return false;

            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.LocalFolder.GetFileAsync(NotificationsFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }

            if (file != null)
            {
                try
                {
                    string content = null;

                    using (await this._notificationFileLock.LockAsync())
                    {
                        content = await file.LoadFileAsync();
                    }

                    if (string.IsNullOrEmpty(content))
                        return false;

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(content);

                    XmlNodeList notifications = doc.SelectNodes("/root/n");

                    foreach (var n in notifications)
                    {
                        Guid guid = Guid.Parse(n.Attributes.GetNamedItem("tile").InnerText);
                        SavedQuery sq = null;

                        foreach (var q in this._queries)
                        {
                            if (q.TileId == guid)
                            {
                                sq = q;
                                break;
                            }
                        }

                        if (sq != null)
                        {
                            sq.Notifications = int.Parse(n.Attributes.GetNamedItem("count").InnerText);
                            sq.RssNewDate = DateTime.Parse(n.Attributes.GetNamedItem("time").InnerText);
                        }
                    }

                    int count = 0;
                    foreach (var q in this.Queries)
                    {
                        count += q.Notifications;
                    }
                    this.Notifications = count;

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debugger.Break();
                    Logger.LogMessage("SavedSearches", "Failed to read NotificationsFileName.xml");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveNotificationsAsync()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("<root>");
                lock (_listLock)
                {
                    for (int i = 0; i < _queries.Count; ++i)
                    {
                        sb.AppendLine(string.Format(@"<n tile=""{0}"" count=""{1}"" time=""{2}""/>",
                            this._queries[i].TileId.ToString(),
                            this._queries[i].Notifications,
                            this._queries[i].RssNewDate
                            ));
                    }
                }
                sb.AppendLine("</root>");

                using (await this._notificationFileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(NotificationsFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("SavedSearches", "Failed to save SavedSearches.xml");
                Logger.LogException(ex);
            }

            return false;
        }

        private AsyncLock _notificationFileLock;
        #endregion

        #region Methods
        void SavedQuery_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Notifications")
            {
                int count = 0;
                foreach (var q in this.Queries)
                {
                    count += q.Notifications;
                }

                this.Notifications = count;
                return;
            }

            this.Dirty = true;
        }

        public void Add(SavedQuery query)
        {
            if (!Settings.Instance.TrackRecentSearches)
                return;

            // Add to the top because it is the most recent
            lock (_listLock)
            {
                _queries.Insert(0, query);
            }

            this.Dirty = true;
        }

        public void Remove(SavedQuery query)
        {
            lock (_listLock)
            {
                this._queries.Remove(query);
            }

            this.Dirty = true;
        }

        public bool Contains(string name)
        {
            foreach (var sq in this._queries)
            {
                if (sq.Name == name)
                    return true;
            }

            return false;
        }

        public SavedQuery Get(string name)
        {
            foreach (var sq in this._queries)
            {
                if (sq.Name == name)
                    return sq;
            }

            return null;
        }
        #endregion

        #region Properties
        public ObservableCollection<SavedQuery> Queries
        {
            get
            {
                return _queries;
            }
            private set
            {
                this.SetProperty(ref this._queries, value);
            }
        }

        public int Notifications
        {
            get
            {
                return this._notifications;
            }
            set
            {
                var x = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
                    () =>
                    {
                        if (this.SetProperty(ref this._notifications, value))
                        {
                            try
                            {
                                if (value == 0)
                                {
                                    BadgeUpdater updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
                                    updater.Clear();
                                }
                                else
                                {
                                    XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                                    XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                                    badgeElement.SetAttribute("value", value.ToString());
                                    BadgeNotification badge = new BadgeNotification(badgeXml);
                                    BadgeUpdater updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
                                    updater.Update(badge);
                                }
                            }
                            catch
                            {
                            }
                        }
                    });
            }
        }
        #endregion

        #region Fields
        ObservableCollection<SavedQuery> _queries;
        object _listLock;
        int _notifications;
        #endregion

        #region Constants
        const string SavedSearchesFileName = "SavedSearches.xml";
        const string NotificationsFileName = "SavedSearchesNotifications.xml";
        #endregion
    }
}