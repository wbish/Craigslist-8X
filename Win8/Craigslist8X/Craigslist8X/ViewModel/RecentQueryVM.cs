using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.CraigslistApi;
using WB.SDK.Logging;

namespace WB.Craigslist8X.ViewModel
{
    public class RecentQueryVM
    {
        #region Initialization
        public RecentQueryVM(Query query)
        {
            Logger.Assert(query != null, "Query is null!");

            _query = query;
        }
        #endregion

        #region Properties
        public Query Query
        {
            get
            {
                return _query;
            }
        }

        public string QueryText
        {
            get
            {
                if (string.IsNullOrEmpty(_query.Text))
                    return "Browsing";
                else
                    return _query.Text;
            }
        }

        public string Context
        {
            get
            {
                return string.Format("{0} @ {1}", _query.Category.Name, _query.City.City);
            }
        }
        #endregion

        #region Fields
        Query _query;
        #endregion
    }
}
