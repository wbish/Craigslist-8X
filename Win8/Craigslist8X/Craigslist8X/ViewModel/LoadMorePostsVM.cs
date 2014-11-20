using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WB.Craigslist8X.ViewModel
{
    class LoadMorePostsVM : PostBase
    {
        public LoadMorePostsVM(QueryResultVM qrvm)
            : base()
        {
            this._qrvm = qrvm;
        }

        public QueryResultVM QueryResultVM
        {
            get
            {
                return this._qrvm;
            }
        }

        public bool LoadingMoreItems
        {
            get
            {
                return this._loadingMoreItems;
            }
            set
            {
                this.SetProperty(ref this._loadingMoreItems, value);
            }
        }

        public override PostType Type
        {
            get { return PostType.LoadMore; }
        }

        QueryResultVM _qrvm;
        bool _loadingMoreItems;
    }
}
