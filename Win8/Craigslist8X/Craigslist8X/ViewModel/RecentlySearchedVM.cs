using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class RecentlySearchedVM
    {
        public RecentlySearchedVM()
        {
            this.RecentQueries = new ObservableCollection<RecentlySearchedQueryVM>((from x in RecentlySearched.Instance.Queries select new RecentlySearchedQueryVM(x)));
        }

        public ObservableCollection<RecentlySearchedQueryVM> RecentQueries
        {
            get;
            set;
        }
    }

    public class RecentlySearchedQueryVM
    {
        public RecentlySearchedQueryVM(Query q)
        {
            this.Query = q;
        }

        public string Label
        {
            get
            {
                if (string.IsNullOrEmpty(this.Query.Text))
                    return string.Format("Browse {0}", this.Query.Category.Name);
                else
                    return string.Format("{0} in {1}", this.Query.Text, this.Query.Category.Name);
            }
        }

        public Query Query
        {
            get;
            set;
        }
    }
}
