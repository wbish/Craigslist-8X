using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Callisto.Controls;

using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class MainMenuOptionsPanel : UserControl, IPanel, IWebViewHost
    {
        public MainMenuOptionsPanel()
        {
            this.InitializeComponent();

            this.buttons = new List<Button>()
            {
                SearchMenuButton,
                BrowseMenuButton,
                RecentlySearchedMenuButton,
                FavoritesMenuButton,
                RecentlyViewedMenuButton,
                CreatePostMenuButton,
                SavedSearchesMenuButton,
                UpgradeMenuButton,
                AccountManagementMenuButton,
            };

            this._vm = new MainOptionsVM();
            this.DataContext = this._vm;
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            await Logger.AssertNull(parent, "MainMenuOptionsPanel should not have a parent");
        }

        public void ToggleWebView(bool visible)
        {
            this._vm.HideWebView = !visible;
        }

        #region Commands
        private void SearchMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Windows.ApplicationModel.Search.SearchPane.GetForCurrentView().Show();
        }

        private async void BrowseMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await MainPage.Instance.ExecuteBrowse(this, null);
        }

        private async void RecentlySearchedMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(RecentlySearchedMenuButton);

            await MainPage.Instance.ExecuteRecentlySearched(this);
        }

        private async void SavedSearchesMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(SavedSearchesMenuButton);

            await MainPage.Instance.ExecuteSavedSearches(this, null);
        }
        
        private async void FavoritesMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(FavoritesMenuButton);

            await MainPage.Instance.ExecuteFavorites(this);
        }

        private async void RecentlyViewedMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(RecentlyViewedMenuButton);

            await MainPage.Instance.ExecuteRecentlyViewed(this);
        }

        private async void CreatePostMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!CityManager.Instance.SearchCitiesDefined)
            {
                MessageDialog dlg = new MessageDialog("Please set your cities and try again.", "Craigslist 8X");
                await dlg.ShowAsync();
                SettingsUI.ShowSearchSettings();
                return;
            }

            this.SetSelection(CreatePostMenuButton);

            await MainPage.Instance.ExecuteChooseAccount(this, ChooseAccountPurpose.CreatePost);
        }

        private async void AccountManagementMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(AccountManagementMenuButton);

            await MainPage.Instance.ExecuteChooseAccount(this, ChooseAccountPurpose.AccountManagement);
        }

        private async void UpgradeMenuButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.SetSelection(UpgradeMenuButton);

            await MainPage.Instance.ExecuteUpgrade(this);
        }

        public void SetPurchasedPro()
        {
            this._vm.ShowAds = false;
        }

        public void SetSelectionSearch()
        {
            this.SetSelection(this.SearchMenuButton);
        }

        public void SetSelectionBrowse()
        {
            this.SetSelection(this.BrowseMenuButton);
        }

        public void SetSelectionCreatePost()
        {
            this.SetSelection(this.CreatePostMenuButton);
        }

        private void SetSelection(Button btn)
        {
            foreach (Button b in this.buttons)
            {
                b.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
            }

            btn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xDD, 0xDD));
        }

        MainOptionsVM _vm;
        List<Button> buttons;
        #endregion

        private void SearchSettings_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SettingsUI.ShowSearchSettings();
        }
    }
}
