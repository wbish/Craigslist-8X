using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    public sealed partial class RecentlyViewedPanel : UserControl, IPanel, IPostList
    {
        public RecentlyViewedPanel()
        {
            this.InitializeComponent();
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            if (!Settings.Instance.TrackRecentlyViewedPosts)
            {
                NoItemsFound.Visibility = Windows.UI.Xaml.Visibility.Visible;
                NoItemsFound.Text = RecentlyViewedOff;
                SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            this._vm = new RecentlyViewedVM();
            this.DataContext = this._vm;

            if (this._vm.PostItems.Count < 1)
            {
                NoItemsFound.Visibility = Windows.UI.Xaml.Visibility.Visible;
                NoItemsFound.Text = NoItemsFoundText;
            }
            else
            {
                await this.SelectItem(this._vm.PostItems.First() as PostVM);
            }

            SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            await this._vm.InitAsync();
        }

        public void SetSelection(PostVM post)
        {
            if (post != null)
            {
                if (this._selected != null)
                {
                    this._selected.Selected = false;
                }

                this._selected = post;
                this._selected.Selected = true;

                this.SearchItemsList.ScrollIntoView(this._selected);
            }
        }

        public ObservableCollection<PostBase> PostItems
        {
            get
            {
                if (this._vm == null)
                    return null;

                return this._vm.PostItems;
            }
        }

        private void SearchItemsList_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ScrollViewer scroller = Utilities.GetVisualChild<ScrollViewer>(SearchItemsList);
            int delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (delta > 0)
            {
                if (scroller.VerticalOffset <= 2.01)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
            else if (delta < 0)
            {
                if (scroller.VerticalOffset >= scroller.ExtentHeight - scroller.ViewportHeight)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
        }

        private async void DeleteButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            RecentlyViewed.Instance.Posts.Clear();

            Utilities.ExecuteNotification("Craigslist 8X", "Successfully cleared recently viewed posts.");

            await MainPage.Instance.ExecuteRecentlyViewed(null);
        }

        private async void SearchItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as PostVM != null)
            {
                await this.SelectItem((sender as FrameworkElement).DataContext as PostVM);
            }
        }

        private async Task SelectItem(PostVM post)
        {
            if (post != null)
            {
                await MainPage.Instance.ExecuteViewPost(this as UIElement, post);

                this.SetSelection(post);
            }
        }

        private RecentlyViewedVM _vm;
        private PostVM _selected;

        const string NoItemsFoundText = "No recently viewed posts.";
        const string RecentlyViewedOff = "Tracking recently viewed posts is disabled. Go to settings to reenable.";
    }
}
