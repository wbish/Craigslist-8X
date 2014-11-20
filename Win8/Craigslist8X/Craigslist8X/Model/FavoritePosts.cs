using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    public class FavoritePosts : BindableBase
    {
        #region Initialization
        static FavoritePosts()
        {
            _instanceLock = new object();
        }

        private FavoritePosts()
        {
            this._listLock = new object();
            this._fileLock = new AsyncLock();
            this.Posts = new ObservableCollection<Post>();
        }
        #endregion

        #region Singleton
        public static FavoritePosts Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new FavoritePosts();

                    return _instance;
                }
            }
        }

        static FavoritePosts _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.RoamingFolder.GetFileAsync(FavoritePostsFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
                return false;
            }

            if (file != null)
            {
                Logger.LogMessage("FavoritePosts", "Loading FavoritePosts.xml");

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

                    lock (this._listLock)
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
                    Logger.LogMessage("FavoritePosts", "Failed to read FavoritePosts.xml");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveAsync()
        {
            Logger.LogMessage("FavoritePosts", "Saving FavoritePosts.xml");

            if (!this.Dirty)
            {
                Logger.LogMessage("FavoritePosts", "No changes to persist to FavoritePosts.xml");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("<root>");
                for (int i = 0; i < _posts.Count && i < SaveCount; ++i)
                {
                    sb.AppendLine(Post.Serialize(_posts[i] as Post));
                }
                sb.Append("</root>");

                using (await this._fileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.RoamingFolder.CreateFileAsync(FavoritePostsFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                this.Dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("FavoritePosts", "Failed to save FavoritePosts.xml");
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
            return this.Posts.Contains(post);
        }

        public void AddPost(Post post)
        {
            // Add to the top because it is the most recent
            lock (this._listLock)
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

                this.Dirty = true;
            }
        }

        public void RemovePost(Post post)
        {
            lock (this._listLock)
            {
                if (this.Posts.Contains(post))
                {
                    this.Posts.Remove(post);
                    this.Dirty = true;
                }
            }
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
        const string FavoritePostsFileName = "FavoritePosts.xml";
        const int SaveCount = 50;
        #endregion
    }
}
