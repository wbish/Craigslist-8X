using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    public sealed partial class SavedSearchesPanel : UserControl, IPanel
    {
        public SavedSearchesPanel()
        {
            this.InitializeComponent();
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            if (SavedSearches.Instance.Queries.Count == 0)
            {
                NoItemsMessage.Text = NoItemsFound;
            }
            else
            {
                this.SearchItemList.ItemsSource = new ObservableCollection<SavedSearchVM>(from x in SavedSearches.Instance.Queries select new SavedSearchVM(x));
            }

            string name = context as string;
            if (!string.IsNullOrEmpty(name))
            {
                SavedQuery sq = SavedSearches.Instance.Get(name);

                if (sq != null)
                {
                    this.SetSelection(sq);
                    await MainPage.Instance.ExecuteSearchQuery(this, QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, sq.Query));
                }
                else
                {
                    MessageDialog dlg = new MessageDialog(string.Format(@"Unable to find the saved query: ""{0}""", name), "Craigslist 8X");
                    await dlg.ShowAsync();
                }
            }
        }

        private void ListView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ScrollViewer scroller = Utilities.GetVisualChild<ScrollViewer>(this.SearchItemList);
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

        private async void SavedSearch_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as SavedSearchVM != null)
            {
                SavedSearchVM ssvm = (SavedSearchVM)container.DataContext;

                this.SetSelection(ssvm.SavedQuery);

                await MainPage.Instance.ExecuteSavedQuery(this, ssvm.SavedQuery);
            }
        }

        private void SetSelection(SavedQuery sq)
        {
            if (this._selected != null)
            {
                this._selected.Selected = false;
            }

            if (sq != null)
            {
                IEnumerable<SavedSearchVM> items = this.SearchItemList.ItemsSource as IEnumerable<SavedSearchVM>;
                var q = (from ssvm in items.Where(x => x.SavedQuery == sq) select ssvm).FirstOrDefault();

                if (q != null)
                {
                    q.Selected = true;
                    this._selected = q;
                }
            }
        }

        private void DeleteSearch_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as SavedSearchVM != null)
            {
                e.Handled = true;
                SavedSearchVM ssvm = (SavedSearchVM)container.DataContext;
                SavedSearches.Instance.Remove(ssvm.SavedQuery);

                this.SearchItemList.ItemsSource = new ObservableCollection<SavedSearchVM>(from x in SavedSearches.Instance.Queries select new SavedSearchVM(x));
            }
        }

        private async void PinSearch_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as SavedSearchVM != null)
            {
                e.Handled = true;
                SavedSearchVM ssvm = (SavedSearchVM)container.DataContext;

                SecondaryTile tile = new SecondaryTile(ssvm.SavedQuery.TileId.ToString(),
                    ssvm.Name,
                    ssvm.Name,
                    string.Format(@"<SavedQuery ID=""{0}"" Name=""{1}"" PinTime=""{2}"" />", ssvm.SavedQuery.TileId.ToString(), Uri.EscapeDataString(ssvm.Name), DateTime.Now),
                    TileOptions.ShowNameOnLogo|TileOptions.ShowNameOnWideLogo,
                    new Uri("ms-appx:///Resources/Logo.png"),
                    new Uri("ms-appx:///Resources/WideLogo.png"));

                await tile.RequestCreateForSelectionAsync(Utilities.GetElementRect(sender as FrameworkElement), Windows.UI.Popups.Placement.Below);
            }
        }

        private SavedSearchVM _selected;

        const string NoItemsFound = @"No saved searches found.

Saved searches will run in the background and Craigslist 8X will notify when there are new ads matching your search.

Next time you want to save a search, look for the ellipses button in the search results pane and select Save Search.";
    }
}
