using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Callisto.Controls;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class SearchResultsPanel : UserControl, IPanel, IPostList, IWebViewHost
    {
        public SearchResultsPanel()
        {
            this.InitializeComponent();

            this.SetView(Settings.Instance.ExpandSearchResults ? View.Grid : View.List);
        }

        #region IPanel
        public async Task AttachContext(object context, IPanel parent)
        {
            if (context is QueryBatch)
            {
                await this.ExecuteQuery(context as QueryBatch);
            }
            else if (context is SavedQuery)
            {
                // If this is a saved query, there a few extra steps we need to take:
                // 1. Make sure we default to the city the user saved the query as.
                // 2. Update notifications to 0
                // 3. Update cached time

                this._sq = (SavedQuery)context;
                List<CraigCity> cities = CityManager.Instance.SearchCities.ToList();
                cities.Remove(this._sq.Query.City);
                cities.Insert(0, this._sq.Query.City);

                QueryBatch qb = QueryBatch.BuildQueryBatch(cities, this._sq.Query);
                await this.ExecuteQuery(qb, this._sq);

                // Reset the notification counter and save the file
                if (this._vm.QuerySucceeded)
                {
                    this._sq.Notifications = 0;

                    DateTime time = DateTime.Now;
                    if (this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).Any())
                    {
                        PostVM vm = (this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).First() as PostVM) as PostVM;
                        await vm.LoadDetailsAsync();
                        if (vm.DetailStatus == Post.PostDetailStatus.Loaded)
                        {
                            time = vm.Post.Timestamp;
                        }
                    }
                    this._sq.CacheDate = time > this._sq.RssNewDate ? time : this._sq.RssNewDate;

                    // Save the list
                    var s = SavedSearches.Instance.SaveAsync();
                    var n = SavedSearches.Instance.SaveNotificationsAsync();
                }
            }
            else
            {
                await Logger.AssertNotReached("Unrecognized search context");
            }
        }
        #endregion

        #region IPostList
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
                this.SearchItemsGrid.ScrollIntoView(this._selected);
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
        #endregion

        #region IWebViewHost
        public void ToggleWebView(bool visible)
        {
            if (!visible)
                AdBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            else
                AdBar.Visibility = App.IsPro ? Windows.UI.Xaml.Visibility.Collapsed : Windows.UI.Xaml.Visibility.Visible;
        }
        #endregion

        #region Query Execution
        public async Task ExecuteQuery(QueryBatch qb, SavedQuery sq = null)
        {
            if (qb == null)
            {
                await Logger.AssertNotReached("why is the querybatch null?");
                return;
            }

            this.SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Visible;
            this.NoResultsMessageList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            this.NoResultsMessageGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            SearchResultsVM oldvm = this._vm;
            this._qb = qb;
            this._vm = new SearchResultsVM(this._qb);
            this.DataContext = this._vm;

            if (oldvm != null)
            {
                this._vm.SelectedCity = oldvm.SelectedCity;
            }

            this.SearchTitle.Text = this._vm.SearchTitle;
            this.FilterButton.IsChecked = this._vm.FiltersSet;
            this.SortButton.IsChecked = this._vm.SortSet;

            this._vm.QueryExecuted += SearchQueryExecuted;

            if (!WinRTXamlToolkit.Net.WebHelper.IsConnectedToInternet())
            {
                this.SetResultMessage(NotReachedCraigslist);
                this.SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            this._tokenSource = new CancellationTokenSource();
            await this._vm.ExecuteQueryAsync(this._tokenSource.Token);

            if (sq != null && this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).Any())
            {
                PostVM vm = (this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).First() as PostVM) as PostVM;

                vm.DetailsLoaded += PostVM_DetailsLoaded;
                await vm.LoadDetailsAsync();

                if (vm.DetailStatus == Post.PostDetailStatus.Loaded)
                {
                    this.PostVM_DetailsLoaded(vm, Post.PostDetailStatus.Loaded);
                }
            }
        }

        public void CancelExecution()
        {
            if (this._tokenSource != null && this.DataContext != null)
                this._tokenSource.Cancel();
        }

        private void PostVM_DetailsLoaded(object sender, Post.PostDetailStatus e)
        {
            PostVM vm = sender as PostVM;

            if (DateTime.Compare(vm.Post.Timestamp, DateTime.Now.Subtract(TimeSpan.FromMinutes(20))) < 1)
            {
                DateTime recent = DateTime.Now.Subtract(TimeSpan.FromMinutes(15));
                this._sq.CacheDate = recent > this._sq.RssNewDate ? recent : this._sq.RssNewDate;
            }
            else
            {
                this._sq.CacheDate = vm.Post.Timestamp > this._sq.RssNewDate ? vm.Post.Timestamp : this._sq.RssNewDate;
            }
        }

        private async void SearchQueryExecuted(object sender, EventArgs e)
        {
            this._vm.QueryExecuted -= SearchQueryExecuted;
            this.SearchProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            // Handle no connectivity
            if (this.SetNoConnectivityMessage())
                return;

            // Handle no results found
            if (this.SetNotFoundMessage(this._vm.SelectedCity))
                return;

            // Handle auto selecting in listview
            if (this._currentView == View.List)
            {
                foreach (var post in this._vm.PostItems)
                {
                    if (post.Type == PostBase.PostType.Post)
                    {
                        await this.SelectItem(post as PostVM);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Header Buttons
        private async void LoadMoreItems_Tapped(object sender, TappedRoutedEventArgs e)
        {
            LoadMorePostsVM vm = (sender as FrameworkElement).DataContext as LoadMorePostsVM;

            vm.LoadingMoreItems = true;

            await vm.QueryResultVM.LoadMoreResults();

            vm.LoadingMoreItems = false;
        }

        private async void SortButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            this.SortButton.IsChecked = this._vm.SortSet;

            if (!this._vm.FiltersSet)
            {
                await new MessageDialog("Sorting requires at least one filter to be set. Please set any filter or search query and try again.", "Craigslist 8X").ShowAsync();
                return;
            }

            MainPage.Instance.ToggleWebView(show: false);

            PopupMenu menu = new PopupMenu();
            menu.Commands.Add(new UICommand() { Label = "Most Recent", Id = Query.SortOrder.Recent });
            menu.Commands.Add(new UICommand() { Label = "Best Match", Id = Query.SortOrder.Match });
            menu.Commands.Add(new UICommand() { Label = "Low Price", Id = Query.SortOrder.LowPrice });
            menu.Commands.Add(new UICommand() { Label = "High Price", Id = Query.SortOrder.HighPrice });
            var result = await menu.ShowForSelectionAsync(WB.Craigslist8X.Common.Utilities.GetElementRect(sender as FrameworkElement), Placement.Below);

            MainPage.Instance.ToggleWebView(show: true);

            if (result == null)
                return;

            Query first = this._qb.Queries.First();

            if (first.Sort != (Query.SortOrder)result.Id)
            {
                Query template = first.Clone();
                template.Sort = (Query.SortOrder)result.Id;
                QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);

                if (this._vm.PostItems != null && this._vm.PostItems.Any())
                {
                    this.SearchItemsGrid.ScrollIntoView(this._vm.PostItems.First());
                    this.SearchItemsList.ScrollIntoView(this._vm.PostItems.First());
                }

                await this.ExecuteQuery(qb);
            }
        }

        private void FilterButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.FilterButton.IsChecked = this._vm.FiltersSet;

            MainPage.Instance.ToggleWebView(show: false);

            Flyout flyout = new Flyout();
            FiltersContainer filters = new FiltersContainer();
            filters.AttachContext(this._vm.FirstQuery);
            flyout.Content = filters;
            flyout.PlacementTarget = FilterButton;
            flyout.Placement = PlacementMode.Bottom;
            flyout.IsOpen = true;
            flyout.Closed += Filters_Closed;
        }

        private async void MoreOptionsButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MainPage.Instance.ToggleWebView(show: false);

            PopupMenu menu = new PopupMenu();
            menu.Commands.Add(new UICommand() { Label = "Save Search", Id = 0 });
            menu.Commands.Add(new UICommand() { Label = "Refresh", Id = 1 });
            var result = await menu.ShowForSelectionAsync(WB.Craigslist8X.Common.Utilities.GetElementRect(sender as FrameworkElement), Placement.Below);

            MainPage.Instance.ToggleWebView(show: true);

            if (result == null)
            {
                return;
            }
            else if ((int)result.Id == 0)
            {
                this.ShowSaveSearch();
            }
            else if ((int)result.Id == 1)
            {
                await this.ExecuteQuery(this._qb);
            }
            else
            {
                await Logger.AssertNotReached("Unknown menu option");
            }
        }

        private void ShowSaveSearch()
        {
            MainPage.Instance.ToggleWebView(show: false);

            Flyout flyout = new Flyout();
            SaveSearch saveSearch = new SaveSearch();
            flyout.Content = saveSearch;
            flyout.PlacementTarget = FilterButton;
            flyout.Placement = PlacementMode.Bottom;
            flyout.IsOpen = true;
            flyout.Closed += SaveSearch_Closed;
        }

        private async void Filters_Closed(object sender, object e)
        {
            MainPage.Instance.ToggleWebView(show: true);

            if (this._qb == null || this._qb.Queries == null || this._qb.Queries.Count < 1)
                return;

            Query first = this._qb.Queries.First();

            Flyout flyout = sender as Flyout;
            flyout.Closed -= Filters_Closed;

            FiltersContainer filterDialog = flyout.Content as FiltersContainer;

            QueryFilters filters;
            if (first.Filters == null && filterDialog.Filters.ListItemsEqual(QueryFilter.GetFilters(first.Category)))
            {
                filters = null;
            }
            else
            {
                filters = new QueryFilters(filterDialog.Filters);
            }

            Query template = new Query(first.City, first.Category, filterDialog.SearchQuery, filters);
            template.Type = filterDialog.TitlesOnly ? Query.QueryType.TitleOnly : Query.QueryType.EntirePost;
            template.HasImage = filterDialog.HasPictures;
            template.Sort = first.Sort;

            QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);

            if (!first.Equals(template))
            {
                RecentlySearched.Instance.AddQuery(qb.Queries.First());

                if (this._vm.PostItems != null && this._vm.PostItems.Any())
                {
                    this.SearchItemsGrid.ScrollIntoView(this._vm.PostItems.First());
                    this.SearchItemsList.ScrollIntoView(this._vm.PostItems.First());
                }

                await this.ExecuteQuery(qb);
            }
        }

        private async void SaveSearch_Closed(object sender, object e)
        {
            Flyout flyout = sender as Flyout;
            SaveSearch saveSearch = flyout.Content as SaveSearch;
            string name = saveSearch.SearchName;

            if (string.IsNullOrEmpty(name))
                return;

            if (SavedSearches.Instance.Contains(name))
            {
                MessageDialog dlg = new MessageDialog("Sorry, you already have a saved search by that name. Try a different name.", "Craigslist 8X");
                await dlg.ShowAsync();
                return;
            }

            Query q = this._vm.FirstQuery.Clone();
            q.City = this._vm.SelectedCity;

            SavedQuery sq = new SavedQuery(q, name);
            DateTime time = DateTime.Now;
            if (this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).Any())
            {
                PostVM vm = (this._vm.PostItems.Where(x => x.Type == PostBase.PostType.Post).First() as PostVM) as PostVM;
                if (vm.DetailStatus == Post.PostDetailStatus.Loaded)
                {
                    time = vm.Post.Timestamp;
                }
            }
            sq.CacheDate = time;

            SavedSearches.Instance.Add(sq);
            Utilities.ExecuteNotification("Craigslist 8X", "Successfully saved search.");
        }

        private void ExpandViewButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (this._currentView == View.List)
            {
                this.SetView(View.Grid);
                Settings.Instance.ExpandSearchResults = true;
            }
            else
            {
                this.SetView(View.List);
                Settings.Instance.ExpandSearchResults = false;
            }
        }
        #endregion

        #region PanelView Fixes
        private void SearchResultsBackground_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            // Prevent manipulation events from bubbling up from the gridview.
            e.Handled = true;
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
        #endregion

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                if (this._vm.PostItems != null && this._vm.PostItems.Any())
                {
                    this.SearchItemsList.ScrollIntoView(this._vm.PostItems.FirstOrDefault());
                    this.SearchItemsGrid.ScrollIntoView(this._vm.PostItems.FirstOrDefault());
                }

                // Handle no results found
                if (this.SetNotFoundMessage(e.AddedItems[0] as CraigCity))
                    return;
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

        private async void SearchItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            if (container != null && container.DataContext as PostVM != null)
            {
                if (this._currentView == View.Grid && this._vm.CollapseOnSelection)
                {
                    this.SetView(View.List);

                    if (Settings.Instance.ShowExpandTip)
                    {
                        Flyout flyout = new Flyout();
                        flyout.Content = new ExpandTip();
                        flyout.Placement = PlacementMode.Top;
                        flyout.PlacementTarget = MainPage.Instance.MainMenu;
                        flyout.RenderTransform = new TranslateTransform() { Y = 65, X = -5 };
                        flyout.IsOpen = true;

                        Settings.Instance.ShowExpandTip = false;
                    }
                }

                await this.SelectItem((sender as FrameworkElement).DataContext as PostVM);
            }
        }

        #region View / Messages
        private void SetView(View view)
        {
            if (view == View.List)
            {
                this.ListViewResultsGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
                this.GridViewResultsGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                MainPage.Instance.SetSearchResultsWidth(this, max: false);
                MainPage.Instance.PanelView.SnapToPanel(this);
                this.ExpandViewButton.IsChecked = false;
                this.SearchItemsList.ScrollIntoView(this._selected);
            }
            else if (view == View.Grid)
            {
                this.ListViewResultsGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                this.GridViewResultsGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
                MainPage.Instance.SetSearchResultsWidth(this, max: true);
                MainPage.Instance.PanelView.SnapToPanel(this);
                this.ExpandViewButton.IsChecked = true;
                this.SearchItemsGrid.ScrollIntoView(this._selected);
            }

            this._currentView = view;
        }

        private bool SetNoConnectivityMessage()
        {
            if (!this._vm.QuerySucceeded)
            {
                this.SetResultMessage(NotReachedCraigslist);
                return true;
            }

            return false;
        }

        private bool SetNotFoundMessage(CraigCity city)
        {
            QueryResultVM qr = this._vm.GetCityQueryResult(city);

            if (qr == null)
            {
                return true;
            }
            if (qr.PostItems.Count < 1)
            {
                if (this._vm.FirstQuery.Mode == Query.SearchMode.Query)
                {
                    this.SetResultMessage(string.Format(NoItemsFoundSearchFormat, qr.QueryResult.Query.Text, qr.QueryResult.Query.Category.Name, qr.QueryResult.Query.City.DisplayName));
                }
                else
                {
                    this.SetResultMessage(string.Format(NoItemsFoundBrowseFormat, qr.QueryResult.Query.Category.Name, qr.QueryResult.Query.City.DisplayName));
                }

                return true;
            }
            else
            {
                NoResultsMessageGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                NoResultsMessageList.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            return false;
        }

        private void SetResultMessage(string message)
        {
            NoResultsMessageList.Visibility = Windows.UI.Xaml.Visibility.Visible;
            NoResultsMessageList.Text = message;
            NoResultsMessageGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            NoResultsMessageGrid.Text = message;
        }
        #endregion

        #region Fields
        SavedQuery _sq;
        QueryBatch _qb;
        SearchResultsVM _vm;
        PostVM _selected;
        CancellationTokenSource _tokenSource;
        View _currentView;
        #endregion

        #region Constants
        enum View
        {
            List,
            Grid,
            None,
        }

        const string NotReachedCraigslist = "Craigslist 8X is having trouble reaching Craigslist. Check your internet connection and try again.";
        const string NoItemsFoundSearchFormat = @"No results found for ""{0}"" in the {1} category in {2}.

Please refine your query, try a different location or change your search category in settings.";

        const string NoItemsFoundBrowseFormat = @"No results found in the {0} category in {1}. 

Please refine your query, try a different location or change your search category in settings.";
        #endregion
    }
}