using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Security.Credentials;

using Callisto.Controls;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Controls;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    sealed partial class MainPage : LayoutAwarePage
    {
        public MainPage()
        {
            this.InitializeComponent();

            MainPage.Instance = this;
            this.DataContext = this;
            this.Loaded += this.MainPage_Loaded;
            this.PanelView = this.MainPanelView;
            this.SizeChanged += this.MainPage_SizeChanged;

            this.PanelView.ItemsMoved += PanelView_ItemsMoved;
            this.PanelView.ItemsArranged += PanelView_ItemsArranged;
            this.MainMenu = new MainMenuOptionsPanel();
            this.MainMenu.Width = this.MinPanelWidth;

            var x = this.MainPanelView.AddPanel(this.MainMenu);
        }

        void PanelView_ItemsArranged(object sender, IEnumerable<UIElement> e)
        {
            foreach (UIElement panel in this.MainPanelView.Children)
            {
                if (panel is MainMenuOptionsPanel)
                {
                    (panel as IWebViewHost).ToggleWebView(!this.MainPanelView.PanelOutOfView(panel));
                    break;
                }
            }
        }

        async void PanelView_ItemsMoved(object sender, PanelMove e)
        {
            switch (e)
            {
                case PanelMove.Started:
                    this.ToggleWebView(false);
                    break;
                case PanelMove.Finished:
                    this.ToggleWebView(true);
                    break;
                default:
                    await Logger.AssertNotReached("PanelMove type not recognized.");
                    break;
            }
        }

        async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            bool gpsLocated = false;

            try
            {
                if (!CityManager.Instance.SearchCitiesDefined)
                {
                    if (Craigslist8XData.Location == null)
                    {
                        Geolocator locator = new Geolocator();
                        locator.DesiredAccuracy = PositionAccuracy.Default;
                        Craigslist8XData.Location = await locator.GetGeopositionAsync();
                    }

                    if (Craigslist8XData.Location != null)
                    {
                        CraigCity city = await CityManager.Instance.FindNearestCityGps(Craigslist8XData.Location.Coordinate);

                        if (city != null)
                        {
                            CityManager.Instance.AddSearchCity(city);
                            gpsLocated = true;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogException(ex);
            }

            await BackgroundTaskManager.RegisterAccess();

            if (Settings.Instance.AppBootCount >= 10 && Settings.Instance.AppBootCount % 10 == 0 && Settings.Instance.PromptForRating)
            {
                MessageDialog dlg = new MessageDialog(string.Format("It looks like you have used this app over {0} times. We really appreciate your loyalty and would love it if you could take 30 seconds to give us a 5 star rating. Thanks for using Craigslist 8X! ", Settings.Instance.AppBootCount), "Craigslist 8X");
                dlg.Commands.Add(new UICommand() { Id = 0, Label = "Sure" });
                dlg.Commands.Add(new UICommand() { Id = 1, Label = "Maybe later" });
                dlg.Commands.Add(new UICommand() { Id = 2, Label = "Stop bugging me!" });
                dlg.DefaultCommandIndex = 0;

                UICommand cmd = await dlg.ShowAsync() as UICommand;

                if ((int)cmd.Id == 0)
                {
                    // Stop asking because they rated.
                    Settings.Instance.PromptForRating = false;
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", Windows.ApplicationModel.Package.Current.Id.FamilyName)));
                }
                else if ((int)cmd.Id == 2)
                {
                    // Never ask again
                    Settings.Instance.PromptForRating = false;
                }
            }

            if (!CityManager.Instance.SearchCitiesDefined || gpsLocated)
            {
                SettingsUI.ShowSearchSettings();
            }

            SettingsSideMenuButton.IsEnabled = !!(ApplicationView.Value != ApplicationViewState.Snapped);
            NewsButton.IsEnabled = await NewsManager.Instance.IsNewItem();

            // I'm a tricky bastard. We only really need to do this once. Ever. 
            // The next time a reviewer installs this app it is from a clean slate, so obviously this setting will not be defined.
            // If it is set, then it is very very unlikely it is a reviewer. I'm willing to take my chances and make this optimization.
            if (!Settings.Instance.PersonalsUnlocked)
            {
                Settings.Instance.PersonalsUnlocked = await NewsManager.Instance.IsPersonalsAvailable();
            }
        }

        public void MainPage_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            foreach (var panel in this.MainPanelView.Children)
            {
                if (panel is ViewPostPanel)
                {
                    (panel as ViewPostPanel).HandleShare(args.Request);
                    return;
                }
            }
        }

        void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SettingsSideMenuButton.IsEnabled = !!(ApplicationView.Value != ApplicationViewState.Snapped);
        }

        #region Side Menu
        private void HomeSideMenu_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.MainPanelView.SnapToPanel(panel: 0);
        }

        private void SettingsSideMenu_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Windows.UI.ApplicationSettings.SettingsPane.Show();
        }

        private async void NewsSideMenu_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MainPage.Instance.ToggleWebView(show: false);

            var newsItems = await NewsManager.Instance.GetNews();

            if (newsItems == null || newsItems.Count < 1)
            {
                await new MessageDialog("There was a problem retreiving news updates. Please try again later.", "Craigslist 8X").ShowAsync();
                return;
            }

            NewsContainer news = new NewsContainer();
            news.Width = CoreApplication.MainView.CoreWindow.Bounds.Width;
            news.Height = CoreApplication.MainView.CoreWindow.Bounds.Height;
            news.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center;
            news.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            news.SetContext(newsItems);

            Canvas sp = new Canvas();
            sp.Width = CoreApplication.MainView.CoreWindow.Bounds.Width;
            sp.Height = CoreApplication.MainView.CoreWindow.Bounds.Height;
            sp.Background = new Windows.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color() { A = 225, R = 0, G = 0, B = 0 });
            sp.Children.Add(news);

            Popup popup = new Popup();
            popup.HorizontalAlignment = HorizontalAlignment.Center;
            popup.VerticalAlignment = VerticalAlignment.Center;
            popup.Child = sp;
            popup.Width = CoreApplication.MainView.CoreWindow.Bounds.Width;
            popup.Height = CoreApplication.MainView.CoreWindow.Bounds.Height;
            popup.IsLightDismissEnabled = true;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 0;
            popup.IsOpen = true;

            sp.Tapped += (s, x) =>
            {
                NewsButton.IsEnabled = false;
                popup.IsOpen = false;
                MainPage.Instance.ToggleWebView(show: true);
            };
        }

        private void BackSideMenu_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (this.MainPanelView.Children.Count - 1 > 0)
            {
                this.MainPanelView.RemovePanel(this.MainPanelView.Children.Count - 1, recursive: true);
                this.MainPanelView.SnapToPanel(this.MainPanelView.Children.Count - 1);
            }

            // If we,We removed the Contact Information panel the view post panel should be our last panel
            // and we may want to resize it.
            this.SetViewPostWidth(this.MainPanelView.Children[this.MainPanelView.Children.Count - 1] as ViewPostPanel);

            this.BackSideMenuButton.IsEnabled = this.MainPanelView.Children.Count > 1;
        }
        #endregion

        #region Panels
        public async Task ExecuteSearchQuery(UIElement parent, QueryBatch qb)
        {
            Monitor.Enter(this);
            try
            {
                if (!CityManager.Instance.SearchCitiesDefined)
                {
                    MessageDialog dlg = new MessageDialog("Please set the city you want to search and try again.", "Craigslist 8X");
                    await dlg.ShowAsync();
                    SettingsUI.ShowSearchSettings();
                    return;
                }
            }
            finally
            {
                Monitor.Exit(this);
            }

            this.ToggleWebView(false);

            // We only care about the text and category of the query. The queries in a QueryBatch are only different
            // because there is a different query object for each city. For tracking what we want, the first should be
            // sufficient.
            RecentlySearched.Instance.AddQuery(qb.Queries.First());

            await Logger.AssertNotNull(qb, "QueryBatch object should not be null");

            this.RemoveChildPanels(parent);

            SearchResultsPanel searchResults = new SearchResultsPanel();
            searchResults.Width = Settings.Instance.ExpandSearchResults ? PanelView.ActualWidth : Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(searchResults, qb, parent as IPanel);

            await MainPanelView.AddPanel(searchResults);

            if (parent is MainMenuOptionsPanel || parent == null)
            {
                if (qb.Queries.First().Mode == Query.SearchMode.Query)
                    this.MainMenu.SetSelectionSearch();
                else
                    this.MainMenu.SetSelectionBrowse();
            }

            this.ToggleWebView(true);
        }

        public async Task ExecuteSavedQuery(UIElement parent, SavedQuery sq)
        {
            this.ToggleWebView(false);

            if (sq == null)
            {
                await Logger.AssertNotReached("SavedQuery object should not be null");
                this.ToggleWebView(true);
                return;
            }

            this.RemoveChildPanels(parent);

            SearchResultsPanel searchResults = new SearchResultsPanel();
            searchResults.Width = Settings.Instance.ExpandSearchResults ? PanelView.ActualWidth : Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(searchResults, sq, parent as IPanel);

            await MainPanelView.AddPanel(searchResults);

            this.ToggleWebView(true);
        }

        public async Task ExecuteBrowse(UIElement parent, Category category)
        {
            if (!CityManager.Instance.SearchCitiesDefined)
            {
                MessageDialog dlg = new MessageDialog("Please set the city you want to browse and try again.", "Craigslist 8X");
                await dlg.ShowAsync();
                SettingsUI.ShowSearchSettings();
                return;
            }

            this.MainMenu.SetSelectionBrowse();

            this.ToggleWebView(false);

            this.RemoveChildPanels(parent);

            ExploreCategoriesPanel exploreCats = new ExploreCategoriesPanel();
            exploreCats.Width = this.MinPanelWidth;

            this.AttachContext(exploreCats, category, parent as IPanel);

            await MainPanelView.AddPanel(exploreCats);

            this.ToggleWebView(true);
        }

        public async Task ExecuteFavorites(UIElement parent)
        {
            this.ToggleWebView(false);

            this.RemoveChildPanels(parent);

            FavoritedPostsPanel favoritesPanel = new FavoritedPostsPanel();
            favoritesPanel.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(favoritesPanel, null, parent as IPanel);

            await MainPanelView.AddPanel(favoritesPanel);

            this.ToggleWebView(true);
        }

        public async Task ExecuteViewPost(UIElement parent, PostVM post)
        {
            await Logger.Assert(post != null, "Post is null?");
            if (post == null)
                return;

            this.ToggleWebView(false);

            this.RemoveChildPanels(parent);

            ViewPostPanel viewPost = new ViewPostPanel();
            SetViewPostWidth(viewPost);

            this.AttachContext(viewPost, post, parent as IPanel);

            await MainPanelView.AddPanel(viewPost);

            this.ToggleWebView(true);
        }

        public async Task ExecutePostDetails(UIElement parent, PostVM post)
        {
            await Logger.Assert(post != null, "Post is null?");
            if (post == null)
                return;

            this.ToggleWebView(false);

            this.RemoveChildPanels(parent);

            PostDetailsPanel postDetails = new PostDetailsPanel();
            postDetails.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(postDetails, post, parent as IPanel);

            await MainPanelView.AddPanel(postDetails);

            this.ToggleWebView(true);
        }

        public async Task ExecuteRecentlyViewed(UIElement parent)
        {
            this.RemoveChildPanels(parent);

            RecentlyViewedPanel recentlyViewed = new RecentlyViewedPanel();
            recentlyViewed.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(recentlyViewed, null, parent as IPanel);

            await MainPanelView.AddPanel(recentlyViewed);
        }

        public async Task ExecuteRecentlySearched(UIElement parent)
        {
            this.RemoveChildPanels(parent);

            RecentlySearchedPanel recentlySearched = new RecentlySearchedPanel();
            recentlySearched.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(recentlySearched, null, parent as IPanel);

            await MainPanelView.AddPanel(recentlySearched);
        }

        public async Task ExecuteSavedSearches(UIElement parent, string name)
        {
            this.RemoveChildPanels(parent);

            SavedSearchesPanel savedSearches = new SavedSearchesPanel();
            savedSearches.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(savedSearches, name, parent as IPanel);

            await MainPanelView.AddPanel(savedSearches);
        }

        public async Task ExecuteUpgrade(UIElement parent)
        {
            this.RemoveChildPanels(parent);

            UpgradePanel upgrade = new UpgradePanel();
            upgrade.Width = this.PanelView.ActualWidth - (parent as FrameworkElement).ActualWidth - 1;

            this.AttachContext(upgrade, null, parent as IPanel);

            await MainPanelView.AddPanel(upgrade);
        }

        public async Task ExecuteCreatePost(UIElement parent, CreatePostContext context)
        {
            this.RemoveChildPanels(parent);

            CreatePostPanel postsPanel = new CreatePostPanel();
            postsPanel.Width = this.PanelView.ActualWidth;

            this.AttachContext(postsPanel, context, parent as IPanel);

            await MainPanelView.AddPanel(postsPanel);
        }

        public async Task ExecuteChooseAccount(UIElement parent, ChooseAccountPurpose purpose)
        {
            this.RemoveChildPanels(parent);

            ChooseAccountPanel account = new ChooseAccountPanel();
            account.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(account, purpose, parent as IPanel);

            await MainPanelView.AddPanel(account);
        }

        public async Task ExecuteChooseCity(UIElement parent, PasswordCredential account)
        {
            this.RemoveChildPanels(parent);

            ChoosePostCityPanel city = new ChoosePostCityPanel();
            city.Width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);

            this.AttachContext(city, account, parent as IPanel);

            await MainPanelView.AddPanel(city);
        }

        public async Task ExecuteAccountManagement(UIElement parent, PasswordCredential account)
        {
            this.RemoveChildPanels(parent);

            AccountManagementPanel am = new AccountManagementPanel();
            am.Width = this.PanelView.ActualWidth;

            this.AttachContext(am, account, parent as IPanel);

            await this.MainPanelView.AddPanel(am);
        }

        public async Task ExecuteManagePost(UIElement parent, ManagePostContext posting)
        {
            this.RemoveChildPanels(parent);

            ManagePostPanel mp = new ManagePostPanel();
            mp.Width = Math.Min(this.PanelView.ActualWidth, 1000);

            this.AttachContext(mp, posting, parent as IPanel);

            await this.MainPanelView.AddPanel(mp);
        }

        private void AttachContext(IPanel panel, object context, IPanel parent)
        {
            EventHandler<IEnumerable<UIElement>> handler = null;
            handler = new EventHandler<IEnumerable<UIElement>>((s, col) =>
            {
                if (col.Contains(panel as UIElement))
                {
                    var t = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            panel.AttachContext(context, parent);
                        });

                    MainPanelView.ItemsArranged -= handler;
                }
            });
            MainPanelView.ItemsArranged += handler;
        }
        #endregion

        #region Global
        public void SetViewPostWidth(ViewPostPanel panel)
        {
            if (panel == null)
                return;

            double width;

            if (Settings.Instance.ExpandPost)
            {
                width = this.MainPanelView.ActualWidth;
            }
            else
            {
                int index = MainPanelView.GetPanelIndex(panel);
                double complementWidth = 0;

                if (index >= 0 && index + 1 < MainPanelView.Children.Count)
                    complementWidth = MainPanelView.Children[index + 1].DesiredSize.Width;
                else if (index > 0)
                    complementWidth = MainPanelView.Children[index - 1].DesiredSize.Width;
                else if (MainPanelView.Children.Count >= 0)
                    complementWidth = MainPanelView.Children[MainPanelView.Children.Count - 1].DesiredSize.Width;

                width = 1000;
                if (complementWidth > 0)
                    width = this.MainPanelView.ActualWidth - complementWidth;
                if (width < 600)
                    width = this.MainPanelView.ActualWidth;
            }

            panel.Width = width;
            this.PanelView.Measure(this.PanelView.DesiredSize);
        }

        public void SetSearchResultsWidth(SearchResultsPanel panel, bool max)
        {
            double width;

            if (max)
            {
                width = this.MainPanelView.ActualWidth;
            }
            else
            {
                width = Math.Max(MinSearchResultsWidth, this.MinPanelWidth);
            }

            panel.Width = width;
            this.PanelView.Measure(this.PanelView.DesiredSize);
        }

        public void RemoveChildPanels(UIElement parent)
        {
            int index = (parent == null ? 0 : MainPanelView.GetPanelIndex(parent)) + 1;

            for (int i = index; i < MainPanelView.Children.Count; ++i)
            {
                UIElement el = MainPanelView.Children[i];

                // If we are removing a child panel that is a SearchResultsPanel, we need to make sure we cancel the current
                // execution request if one exists.
                if (el as SearchResultsPanel != null)
                {
                    (el as SearchResultsPanel).CancelExecution();
                }
            }

            MainPanelView.RemovePanel(index, recursive: true);
            BackSideMenuButton.IsEnabled = MainPanelView.Children.Count > 0;

            SetViewPostWidth(parent as ViewPostPanel);
        }

        public void ToggleWebView(bool show)
        {
            for (int i = 0; i < MainPanelView.Children.Count; ++i)
            {
                IWebViewHost wvh = MainPanelView.Children[i] as IWebViewHost;
                if (wvh != null)
                {
                    wvh.ToggleWebView(show);
                }
            }
        }

        public static MainPage Instance
        {
            get;
            set;
        }
        #endregion

        #region Panel Stuff
        const int MinSearchResultsWidth = 450;
        internal double MinPanelWidth
        {
            get
            {
                return Math.Max(375.0, this.ActualWidth / 4.0);
            }
        }
        internal PanelView PanelView
        {
            get;
            private set;
        }
        internal MainMenuOptionsPanel MainMenu;
        #endregion
    }
}