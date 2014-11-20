using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.ViewModel
{
    public class PostVM : PostBase
    {
        #region Initialization
        public PostVM(QueryResultVM qr, Post post)
            : this(post)
        {
            if (qr == null)
                throw new ArgumentNullException("qr");

            this._qr = qr;
        }

        public PostVM(Post post)
            : base()
        {
            if (post == null)
                throw new ArgumentNullException("post");

            this._post = post;
            this.IsLoading = true;

            RecentlyViewed.Instance.PropertyChanged += RecentlyViewed_PropertyChanged;
        }
        #endregion

        #region Methods
        public async Task LoadDetailsAsync()
        {
            if (this._post.DetailStatus == Post.PostDetailStatus.Loaded || this._post.DetailStatus == Post.PostDetailStatus.Loading)
            {
                this.IsLoading = false;
                return;
            }

            await this._post.LoadDetailsAsync();

            this.OnPropertyChanged("DetailStatus");

            if (this._post.DetailStatus == CraigslistApi.Post.PostDetailStatus.Loaded)
            {
                this.OnPropertyChanged("Email");
                this.OnPropertyChanged("Phone");
                this.OnPropertyChanged("Timestamp");
                this.OnPropertyChanged("PostAge");
                this.OnPropertyChanged("UserHtml");
                this.OnPropertyChanged("ShortDescription");
                this.OnPropertyChanged("Pictures");
                this.OnPropertyChanged("Thumbnail");
            }

            this.IsLoading = false;

            if (this.DetailsLoaded != null)
            {
                this.DetailsLoaded(this, this._post.DetailStatus);
            }
        }
        #endregion

        #region Events / Handlers
        public event EventHandler<Post.PostDetailStatus> DetailsLoaded;

        private void RecentlyViewed_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.OnPropertyChanged("Visited");
        }
        #endregion

        #region Properties
        public override PostBase.PostType Type
        {
            get { return PostType.Post; }
        }

        public Post Post
        {
            get
            {
                return _post;
            }
        }

        public Post.PostDetailStatus DetailStatus
        {
            get
            {
                return _post.DetailStatus;
            }
            set
            {
                this._post.DetailStatus = value;
            }
        }

        public bool IsLoading
        {
            get
            {
                return this._isLoading;
            }
            set
            {
                this.SetProperty(ref this._isLoading, value);
            }
        }

        public bool Favorite
        {
            get
            {
                return FavoritePosts.Instance.Contains(this._post);
            }
            set
            {
                if (value)
                {
                    FavoritePosts.Instance.AddPost(this._post);
                }
                else
                {
                    FavoritePosts.Instance.RemovePost(this._post);
                }

                this.OnPropertyChanged("Favorite");
            }
        }

        public bool Selected
        {
            get
            {
                return this._isSelected;
            }
            set
            {
                this.SetProperty(ref this._isSelected, value);
            }
        }

        public QueryResultVM QueryResult
        {
            get
            {
                return _qr;
            }
        }
        #endregion

        #region Post Properties
        public bool HasThumbnail
        {
            get
            {
                return _post.ThumbnailUri != null;
            }
        }

        public Uri Thumbnail
        {
            get
            {
                return this._post.ThumbnailUri;
            }
        }

        public bool HasImages
        {
            get
            {
                return this._post.HasImages;
            }
        }

        public bool HasPictures
        {
            get
            {
                return this._post.HasPictures;
            }
        }

        public bool HasMap
        {
            get
            {
                return this._post.HasMap;
            }
        }

        public List<Uri> Pictures
        {
            get
            {
                return this._post.Pictures;
            }
        }

        public string Title
        {
            get
            {
                return _post.Title;
            }
        }

        public string ShortDate
        {
            get
            {
                return _post.ShortDate;
            }
        }

        public string Location
        {
            get
            {
                return WB.SDK.Utilities.HtmlToText(_post.Location);
            }
        }

        public string Price
        {
            get
            {
                if (_post.Price != 0)
                    return string.Format("{0:C}", _post.Price);
                else
                    return null;
            }
        }

        public string ShortDescription
        {
            get
            {
                return _post.PostText;
            }
        }

        public string UserHtml
        {
            get
            {
                return _post.UserHtml;
            }
        }

        public string Url
        {
            get
            {
                return this._post.Url == null ? null : this._post.Url.ToString();
            }
        }

        public string Email
        {
            get
            {
                return this._post.Email;
            }
        }

        public string Phone
        {
            get
            {
                return this._post.Phone;
            }
        }

        public string Timestamp
        {
            get
            {
                if (this._post.Timestamp == DateTime.MinValue)
                    return string.Empty;
                else
                    return _post.Timestamp.ToString();
            }
        }

        public string PostAge
        {
            get
            {
                if (_post.DetailStatus != CraigslistApi.Post.PostDetailStatus.Loaded)
                {
                    return this.ShortDate;
                }

                TimeSpan age = DateTime.Now - this._post.Timestamp;

                if (this._post.Timestamp.Year == 1) // Means we didn't get anything 
                    return this.ShortDate;
                if ((int)age.TotalDays > 7)
                    return this._post.Timestamp.ToString("yyyy-MM-dd");
                else if ((int)age.TotalDays > 0)
                    return string.Format("{0}d ago", (int)age.TotalDays);
                else if ((int)age.TotalHours > 0)
                    return string.Format("{0}h ago", (int)age.TotalHours);
                else
                    return string.Format("{0}m ago", (int)age.TotalMinutes);
            }
        }

        public bool Visited
        {
            get
            {
                return RecentlyViewed.Instance.Contains(this.Post);
            }
        }
        #endregion

        #region Fields
        QueryResultVM _qr;
        Post _post;
        bool _isLoading;
        bool _isSelected;
        #endregion
    }
}