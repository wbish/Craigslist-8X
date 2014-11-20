using System;
using WB.Craigslist8X.Common;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace WB.Craigslist8X.ViewModel
{
    public class PostingVM : BindableBase
    {
        public PostingStatus Status
        {
            get;
            set;
        }

        public Brush StatusColor
        {
            get
            {
                switch (Status)
                {
                    case PostingStatus.Active:
                        return new SolidColorBrush(Colors.LightGreen);
                    case PostingStatus.Deleted:
                        return new SolidColorBrush(Colors.Pink);
                    case PostingStatus.Expired:
                        return new SolidColorBrush(Color.FromArgb(0xff, 0xcc, 0x99, 0xff));
                    case PostingStatus.Flagged:
                        return new SolidColorBrush(Colors.Yellow);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string Title
        {
            get;
            set;
        }

        public string Price
        {
            get;
            set;
        }

        public long PostingId
        {
            get;
            set;
        }

        public Uri Manage
        {
            get;
            set;
        }

        public string City
        {
            get;
            set;
        }

        public string Category
        {
            get;
            set;
        }

        public DateTime Date
        {
            get;
            set;
        }
    }

    public enum PostingStatus
    {
        Active,     // lightgreen
        Deleted,    // pink
        Expired,    // #cc99ff
        Flagged,    // 
    }
}
