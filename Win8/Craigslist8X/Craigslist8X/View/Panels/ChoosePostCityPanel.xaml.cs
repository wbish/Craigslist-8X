using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Security.Credentials;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.View;

namespace WB.Craigslist8X.View
{
    public sealed partial class ChoosePostCityPanel : UserControl, IPanel
    {
        public ChoosePostCityPanel()
        {
            this.InitializeComponent();
        }

        public Task AttachContext(object context, IPanel parent)
        {
            this._account = context as PasswordCredential;

            if (CityManager.Instance.SearchCitiesDefined)
            {
                IEnumerable<CraigCity> cities = (from x in CityManager.Instance.SearchCities select CityManager.Instance.GetCityByUri(x.Location));
                CityItemsList.ItemsSource = (from x in cities select x).Distinct();
            }

            return null;
        }

        private void ListView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ScrollViewer scroller = Utilities.GetVisualChild<ScrollViewer>(this.CityItemsList);
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

        private async void CreatePostCity_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as CraigCity != null)
            {
                CreatePostContext context = new CreatePostContext();
                context.Account = this._account;
                context.City = (sender as FrameworkElement).DataContext as CraigCity;

                await MainPage.Instance.ExecuteCreatePost(this, context);
            }
        }

        PasswordCredential _account;
    }
}
