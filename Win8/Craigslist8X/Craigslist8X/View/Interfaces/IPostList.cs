using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    interface IPostList
    {
        void SetSelection(PostVM post);

        ObservableCollection<PostBase> PostItems
        {
            get;
        }
    }
}
