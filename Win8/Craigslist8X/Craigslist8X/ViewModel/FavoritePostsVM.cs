using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.Common;

namespace WB.Craigslist8X.ViewModel
{
    class FavoritePostsVM : BindableBase
    {
        public FavoritePostsVM()
        {
            this.PostItems = new ObservableCollection<PostBase>();

            foreach (Post p in FavoritePosts.Instance.Posts)
            {
                this.PostItems.Add(new PostVM(p));
            }
        }

        public async Task InitAsync()
        {
            const int pageLoad = 5;
            for (int i = 0; i < this._postItems.Count; i += pageLoad)
            {
                await Task.WhenAll(from item in this._postItems.ToList().GetRange(i, Math.Min(this._postItems.Count - i, pageLoad)) select ((PostVM)item).LoadDetailsAsync());
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

        private ObservableCollection<PostBase> _postItems;
    }
}
