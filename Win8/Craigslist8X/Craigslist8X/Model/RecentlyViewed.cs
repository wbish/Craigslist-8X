using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
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
    public class RecentlyViewed : BindableBase, IStorageBacked
    {
        #region Initialization
        static RecentlyViewed()
        {
            _instanceLock = new object();
        }

        private RecentlyViewed()
        {
            this.Posts = new ObservableCollection<Post>();
            this._listLock = new object();
            this._fileLock = new AsyncLock();

            this.Posts.CollectionChanged += Posts_CollectionChanged;
        }

        void Posts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.OnPropertyChanged("Posts");
        }
        #endregion

        #region Singleton
        public static RecentlyViewed Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new RecentlyViewed();

                    return _instance;
                }
            }
        }

        static RecentlyViewed _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.RoamingFolder.GetFileAsync(RecentlyViewedFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }

            if (file != null)
            {
                Logger.LogMessage("RecentlyViewed", "Loading RecentlyViewed.xml");

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

                    XmlNodeList queries = doc.SelectNodes("/root/p");

                    lock (_listLock)
                    {
                        this.Posts.Clear();

                        foreach (var qn in queries)
                        {
                            Post post = Post.Deserialize(qn.GetXml());
                            this.Posts.Add(post);
                        }
                    }

                    this.Dirty = false;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debugger.Break();
                    Logger.LogMessage("RecentlyViewed", "Failed to read RecentlyViewed.xml");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveAsync()
        {
            Logger.LogMessage("RecentlyViewed", "Saving RecentlyViewed.xml");

            if (!this.Dirty)
            {
                Logger.LogMessage("RecentlyViewed", "No changes to persist to RecentlyViewed.xml");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("<root>");
                lock (_listLock)
                {
                    for (int i = 0; i < _posts.Count && i < SaveCount; ++i)
                    {
                        sb.AppendLine(Post.Serialize(_posts[i] as Post));
                    }
                }
                sb.Append("</root>");

                using (await this._fileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.RoamingFolder.CreateFileAsync(RecentlyViewedFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                this.Dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("RecentlyViewed", "Failed to save RecentlyViewed.xml");
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
        public bool Contains(Post post)
        {
            lock (this._listLock)
            {
                foreach (var p in this.Posts)
                {
                    if (p.Url.Equals(post.Url))
                        return true;
                }
            }

            return false;
        }

        public void AddPost(Post post)
        {
            if (!Settings.Instance.TrackRecentlyViewedPosts)
                return;

            // Add to the top because it is the most recent
            lock (_listLock)
            {
                _posts.Insert(0, post);

                // It could be the case that we have searched for this item before. If that is the case, then
                // just remove the old dupe from the list.
                for (int i = 1; i < this.Posts.Count; ++i)
                {
                    Post p = this.Posts[i] as Post;

                    if (post.Url == p.Url)
                    {
                        this.Posts.RemoveAt(i);
                        break;
                    }
                }
            }

            this.OnPropertyChanged("Posts");
            this.Dirty = true;
        }
        #endregion

        #region Properties
        public ObservableCollection<Post> Posts
        {
            get
            {
                return _posts;
            }
            private set
            {
                this.SetProperty(ref this._posts, value);
            }
        }
        #endregion

        #region Fields
        ObservableCollection<Post> _posts;
        object _listLock;
        #endregion

        #region Constants
        const string RecentlyViewedFileName = "RecentlyViewed.xml";
        const int SaveCount = 50;
        #endregion
    }
}