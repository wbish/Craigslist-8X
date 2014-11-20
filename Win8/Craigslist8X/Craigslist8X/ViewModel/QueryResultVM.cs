using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.ViewModel
{
    public class QueryResultVM : BindableBase
    {
        #region Initialization
        public QueryResultVM(SearchResultsVM srvm, QueryResult qr)
        {
            if (qr == null)
                throw new ArgumentNullException("qr");
            if (srvm == null)
                throw new ArgumentNullException("srvm");

            this._srvm = srvm;
            this._qr = qr;
            this._postItems = new ObservableCollection<PostBase>();

            #pragma warning disable 4014
            this.SyncResults();
            #pragma warning restore 4014
        }
        #endregion

        #region Methods
        public async Task<bool> LoadMoreResults()
        {
            await Logger.Assert(!this._qr.DoneLoading, "We are already done loading. Why are we trying to load more?");
            if (this._qr.DoneLoading)
                return false;

            await this._qr.LoadMoreAsync();
            await this.SyncResults();

            return true;
        }

        private async Task SyncResults()
        {
            // Before we start syncing we need to remove the last "Load more items" fake post.
            if (this._postItems.Count > 0 && this._postItems[this._postItems.Count()-1].Type == PostBase.PostType.LoadMore)
            {
                this._postItems.Remove(this._postItems.Last());
            }

            List<PostVM> itemsAdded = new List<PostVM>();

            if (this._postItems.Count < this._qr.Count)
            {
                for (int i = this._postItems.Count; i < this._qr.Posts.Count; ++i)
                {
                    // Add the item to the serialized list.
                    PostVM vm = new PostVM(this, this._qr.Posts[i]);
                    itemsAdded.Add(vm);
                    this._postItems.Add(vm);
                }
            }

            if (!this._qr.DoneLoading)
            {
                this._postItems.Add(new LoadMorePostsVM(this));
            }

            const int pageLoad = 20;
            for (int i = 0; i < itemsAdded.Count; i += pageLoad)
            {
                await Task.WhenAll(from item in itemsAdded.GetRange(i, Math.Min(pageLoad, itemsAdded.Count - i)) select item.LoadDetailsAsync());
            }
        }
        #endregion

        #region Properties
        public bool DoneLoading
        {
            get
            {
                return _qr.DoneLoading;
            }
        }

        public ObservableCollection<PostBase> PostItems
        {
            get
            {
                return this._postItems;
            }
            set
            {
                this.SetProperty(ref this._postItems, value);
            }
        }

        public SearchResultsVM SearchResults
        {
            get
            {
                return _srvm;
            }
        }

        public QueryResult QueryResult
        {
            get
            {
                return _qr;
            }
        }
        #endregion

        #region Fields
        ObservableCollection<PostBase> _postItems;
        QueryResult _qr;
        SearchResultsVM _srvm;
        #endregion
    }
}
