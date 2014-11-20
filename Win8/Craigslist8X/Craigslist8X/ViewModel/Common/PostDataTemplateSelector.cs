using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class PostDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SearchItemPostThumbnail
        {
            get;
            set;
        }

        public DataTemplate SearchItemPostText
        {
            get;
            set;
        }

        public DataTemplate SearchItemLoadMore
        {
            get;
            set;
        }

        public DataTemplate SearchItemPostSimple
        {
            get;
            set;
        }
        
        public DataTemplate SearchItemAd
        {
            get;
            set;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;
            Frame root = Window.Current.Content as Frame;

            if (element != null && item != null && item is PostBase)
            {
                PostBase post = item as PostBase;

                if (Settings.Instance.DetailedSearchResults)
                {
                    PostVM p = post as PostVM;

                    switch (post.Type)
                    {
                        case PostBase.PostType.Post:
                            if (p.HasThumbnail || p.HasPictures || p.HasImages)
                                return this.SearchItemPostThumbnail;
                            else
                                return this.SearchItemPostText;
                        case PostBase.PostType.LoadMore:
                            return this.SearchItemLoadMore;
                        case PostBase.PostType.Ad:
                            return this.SearchItemAd;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    switch (post.Type)
                    {
                        case PostBase.PostType.Post:
                            return this.SearchItemPostSimple;
                        case PostBase.PostType.LoadMore:
                            return this.SearchItemLoadMore;
                        case PostBase.PostType.Ad:
                            return this.SearchItemAd;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return null;
        }
    }
}
