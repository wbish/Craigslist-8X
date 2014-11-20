using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;

using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.Model
{
    public class RecentlySearched : BindableBase, IStorageBacked
    {
        #region Initialization
        static RecentlySearched()
        {
            _instanceLock = new object();
        }

        private RecentlySearched()
        {
            this.Queries = new ObservableCollection<Query>();
            this._listLock = new object();
            this._fileLock = new AsyncLock();
        }
        #endregion

        #region Singleton
        public static RecentlySearched Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new RecentlySearched();

                    return _instance;
                }
            }
        }

        static RecentlySearched _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.RoamingFolder.GetFileAsync(RecentlySearchedFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }

            if (file != null)
            {
                Logger.LogMessage("RecentlySearched", "Loading RecentlySearched.xml");

                try
                {
                    string content = null;

                    using (await this._fileLock.LockAsync())
                    {
                        content = await file.LoadFileAsync();
                    }

                    if (string.IsNullOrEmpty(content))
                        return true;

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(content);

                    XmlNodeList queries = doc.SelectNodes("/root/q");
                    lock (_listLock)
                    {
                        this.Queries.Clear();

                        foreach (var qn in queries)
                        {
                            Query q = Query.Deserialize(qn.GetXml());
                            this.Queries.Add(q);
                        }
                    }

                    this.Dirty = false;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debugger.Break();
                    Logger.LogMessage("RecentlySearched", "Failed to read RecentlySearched.xml");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveAsync()
        {
            Logger.LogMessage("RecentlySearched", "Saving RecentlySearched.xml");

            if (!this.Dirty)
            {
                Logger.LogMessage("RecentlySearched", "No changes to persist to RecentlySearched.xml");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<root>");
                lock (_listLock)
                {
                    for (int i = 0; i < _queries.Count && i < SaveCount; ++i)
                    {
                        sb.AppendLine(Query.Serialize(_queries[i] as Query));
                    }
                }
                sb.Append("</root>");

                using (await this._fileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.RoamingFolder.CreateFileAsync(RecentlySearchedFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                this.Dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("RecentlySearched", "Failed to save RecentlySearched.xml");
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

        #region Methods
        public void AddQuery(Query query)
        {
            if (!Settings.Instance.TrackRecentSearches)
                return;
                        
            // Add to the top because it is the most recent
            lock (_listLock)
            {
                _queries.Insert(0, query);

                // It could be the case that we have searched for this item before. If that is the case, then
                // just remove the old dupe from the list.
                for (int i = 1; i < this.Queries.Count; ++i)
                {
                    Query q = this.Queries[i] as Query;

                    if (query.Text == q.Text && query.Category.Equals(q.Category))
                    {
                        this.Queries.RemoveAt(i);
                        break;
                    }
                }
            }

            this.Dirty = true;
        }
        #endregion

        #region Properties
        public ObservableCollection<Query> Queries
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
        #endregion

        #region Fields
        ObservableCollection<Query> _queries;
        object _listLock;
        #endregion

        #region Constants
        const string RecentlySearchedFileName = "RecentlySearched.xml";
        const int SaveCount = 50;
        #endregion
    }
}