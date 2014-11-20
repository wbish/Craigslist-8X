using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Notifications;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    public sealed partial class RecentlySearchedPanel : UserControl, IPanel
    {
        public RecentlySearchedPanel()
        {
            this.InitializeComponent();
        }

        public Task AttachContext(object context, IPanel parent)
        {
            if (!Settings.Instance.TrackRecentSearches)
            {
                NoItemsFound.Visibility = Windows.UI.Xaml.Visibility.Visible;
                NoItemsFound.Text = RecentSearchesOff;
                SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return null;
            }

            this.vm = new RecentlySearchedVM();
            this.DataContext = this.vm;

            SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            if (this.vm.RecentQueries.Count == 0)
            {
                SearchItemsList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                NoItemsFound.Visibility = Windows.UI.Xaml.Visibility.Visible;
                NoItemsFound.Text = "No recent searches found.";
            }

            return null;
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
            RecentlySearched.Instance.Queries.Clear();

            var template = ToastTemplateType.ToastText02;
            var content = ToastNotificationManager.GetTemplateContent(template);
            content.SelectSingleNode("/toast/visual/binding/text[@id='1']").InnerText = "Craigslist 8X";
            content.SelectSingleNode("/toast/visual/binding/text[@id='2']").InnerText = "Successfully cleared recently searched.";

            ToastNotification toast = new ToastNotification(content);
            ToastNotificationManager.CreateToastNotifier().Show(toast);

            await MainPage.Instance.ExecuteRecentlySearched(null);
        }

        private async void RecentQuery_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as RecentlySearchedQueryVM != null)
            {
                this.SetSelection(sender as Border);

                RecentlySearchedQueryVM vm = (sender as FrameworkElement).DataContext as RecentlySearchedQueryVM;

                Query template = new Query(CityManager.Instance.SearchCities.First(), vm.Query.Category, vm.Query.Text);
                template.HasImage = Settings.Instance.OnlyShowPostsPictures;
                QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);

                await MainPage.Instance.ExecuteSearchQuery(this, qb);
            }
        }

        private void SetSelection(Border item)
        {
            if (this._selected != null)
            {
                this._selected.Background = new SolidColorBrush(Colors.Transparent);
            }

            this._selected = item;

            if (item != null)
            {
                this._selected.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xDD, 0xDD));
            }
        }

        RecentlySearchedVM vm;
        Border _selected;

        private const string RecentSearchesOff = "Tracking recent searches is disabled. Go to settings to reenable.";
    }
}
