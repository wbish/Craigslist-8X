using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

using Callisto.Controls;

using Microsoft.Live;

using Newtonsoft.Json;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class ViewPostPanel : UserControl, IPanel, IWebViewHost
    {
        public ViewPostPanel()
        {
            this.InitializeComponent();

            this.TogglePostNavBar(Settings.Instance.ShowPostNavBar);
        }

        #region IWebViewHost
        public void ToggleWebView(bool visible)
        {
            // We only use the webview when there are images
            if (this._post != null && this._post.Post.HasImages)
            {
                if (visible)
                {
                    // Show the webview
                    PostWebView.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    PostWebViewRect.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
                else
                {
                    WebViewBrush brush = new WebViewBrush();
                    brush.SourceName = "PostWebView";
                    brush.SetSource(PostWebView);

                    PostWebViewRect.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    PostWebViewRect.Fill = brush;
                    PostWebView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }
        #endregion

        #region IPanel
        public async Task AttachContext(object context, IPanel parent)
        {
            this.ExpandPostButton.IsChecked = Settings.Instance.ExpandPost;

            // If the parent panel is not a list, then hide the previous and next buttons
            this._parentList = parent as IPostList;
            if (this._parentList == null)
            {
                this.PreviousPostBarButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                this.NextPostBarButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            await this.ShowPost(context as PostVM);
        }
        #endregion

        public async Task ShowPost(PostVM post)
        {
            this._post = post;
            this.DataContext = this._post;

            RecentlyViewed.Instance.AddPost(this._post.Post);

            await this.LoadPost();
        }

        public void HandleShare(DataRequest request)
        {
            request.Data.Properties.Title = this._post.Title;
            request.Data.Properties.Description = this._post.ShortDescription;
            request.Data.SetUri(new Uri(this._post.Url));

            if (this._post.HasThumbnail)
            {
                request.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromUri(this._post.Post.ThumbnailUri);
            }
        }

        private async Task LoadPost()
        {
            this.ProgressBar.Visibility = Visibility.Visible;
            PicturesGrid.ItemsSource = null;

            if (this._post.Post.DetailStatus == CraigslistApi.Post.PostDetailStatus.NotLoaded)
            {
                await this._post.LoadDetailsAsync();
            }

            this._post.DetailsLoaded += DetailsLoaded;

            if (this._post.DetailStatus == Post.PostDetailStatus.Loaded)
            {
                this.ShowContent();
                this._post.DetailsLoaded -= DetailsLoaded;
            }
            else if (this._post.DetailStatus != Post.PostDetailStatus.Loading)
            {
                this.ShowLoadError();
            }
        }

        private void DetailsLoaded(object sender, Post.PostDetailStatus e)
        {
            this.ShowContent();
            this._post.DetailsLoaded -= DetailsLoaded;
        }

        private async void SaveToOneNote_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var scopes = new string[] { "wl.signin", "wl.basic", "Office.onenote_create" };
            var client = new LiveAuthClient();
            var result = await client.InitializeAsync(scopes);

            // We may already be connected without logging in (SSO)
            if (result.Status != LiveConnectSessionStatus.Connected)
            {
                result = await client.LoginAsync(scopes);
            }

            if (result.Status != LiveConnectSessionStatus.Connected)
            {
                await new Windows.UI.Popups.MessageDialog("Could not login to your Microsoft Account.", "Authentication Failed").ShowAsync();
                return;
            }

            using (var httpClient = new HttpClient())
            {
                bool success = true;
                var img = new BitmapImage(new Uri("ms-appx:///Resources/OneNotePurple.png"));
                SaveToOneNoteButton.IsChecked = true;
                SaveToOneNoteButton.Content = new Image() { Source = img, Width = 36, Height = 36 };

                var postBostText = string.Format(OneNotePostContent, this._post.Title, DateTime.UtcNow, this._post.Url);
                var presentationPart = new StringContent(postBostText, System.Text.Encoding.UTF8, "application/xhtml+xml");
                MultipartFormDataContent multipart = new MultipartFormDataContent("NewPart");
                multipart.Add(presentationPart, "Presentation");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.onenote.com/api/v1.0/pages");
                request.Content = multipart;
                request.Headers.Add("Authorization", "Bearer " + result.Session.AccessToken);

                try
                {
                    var response = await httpClient.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        dynamic responseObject = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                        ShowSaveToOneNoteToast(string.Format("Successfully finished saving '{0}' to OneNote", this._post.Title), responseObject.links.oneNoteWebUrl.href.ToString());
                    }
                    else
                    {
                        success = false;
                        ShowSaveToOneNoteToast(string.Format("Failed saving '{0}' to OneNote. Please try again.", this._post.Title));
                    }
                }
                catch (Exception)
                {
                    success = false;
                    ShowSaveToOneNoteToast(string.Format("Failed saving '{0}' to OneNote. Please try again.", this._post.Title));
                }
                finally
                {
                    SaveToOneNoteButton.IsChecked = success;
                }
            }
        }

        private void ShowSaveToOneNoteToast(string msg, string arg = null)
        {
            var template = ToastTemplateType.ToastText02;
            var toastContent = ToastNotificationManager.GetTemplateContent(template);
            toastContent.SelectSingleNode("/toast/visual/binding/text[@id='1']").InnerText = "Craigslist 8X";
            toastContent.SelectSingleNode("/toast/visual/binding/text[@id='2']").InnerText = msg;

            if (!string.IsNullOrEmpty(arg))
            {
                ((XmlElement)toastContent.SelectSingleNode("/toast")).SetAttribute("launch",
                    string.Format("{{\"type\":\"toast\",\"clientUrl\":\"{0}\"}}", arg));
            }

            ToastNotification toast = new ToastNotification(toastContent);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }


        private void ToggleExpandPost_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Settings.Instance.ExpandPost = !Settings.Instance.ExpandPost;
            this.ExpandPostButton.IsChecked = Settings.Instance.ExpandPost;
            MainPage.Instance.SetViewPostWidth(this);
            MainPage.Instance.PanelView.SnapToPanel(this);
        }

        private void ToggleFavorite_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this._post.Favorite = !this._post.Favorite;
        }

        private async void ShowDetails_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await MainPage.Instance.ExecutePostDetails(this, this._post);
            MainPage.Instance.SetViewPostWidth(this);
        }

        private void ShowContent()
        {
            this.ProgressBar.Visibility = Visibility.Collapsed;
            this.TitleHeader.Text = this._post.Title;

            this.PostWebView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            this.PostTextView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            // If we have rich context (read: img tags) in the post, then we use the WebView control to display the content.
            // otherwise
            if (this._post.HasImages)
            {
                PostWebView.Visibility = Visibility.Visible;
                PostWebView.NavigateToString(this._post.UserHtml);
            }
            else
            {
                PostTextView.Visibility = Visibility.Visible;
                PostTextView.Text = this._post.ShortDescription ?? string.Empty;
            }

            if (this._post.HasPictures)
            {
                PicturesGrid.ItemsSource = this._post.Pictures;
            }
        }

        private void ShowLoadError()
        {
            Logger.LogMessage("ViewPostPanel", "Failed to load post details: {0}", this._post.Post.Url);

            this.ProgressBar.Visibility = Visibility.Collapsed;
            this.TitleHeader.Text = this._post.Title;
            this.PostTextView.Visibility = Visibility.Visible;
            this.PostTextView.Text = "We encountered an error while trying to load the post. Please check your network connectivity and try again.";
            this.PostTextView.FontSize = 24;
            this.PostWebView.MaxWidth = this.Width;
            this.PicturesGrid.Visibility = Visibility.Collapsed;
        }

        #region Post Flagging
        private void FlagPost_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MenuItem mmi = new MenuItem() { Text = "Miscategorized", CommandParameter = FlagCode.Miscategorized };
            MenuItem pmi = new MenuItem() { Text = "Prohibited", CommandParameter = FlagCode.Prohibited };
            MenuItem smi = new MenuItem() { Text = "Spam / Overpost", CommandParameter = FlagCode.Spam };
            MenuItem bmi = new MenuItem() { Text = "Best of Craigslist", CommandParameter = FlagCode.BestOf };

            mmi.Tapped += FlagMenuItem_Tapped;
            pmi.Tapped += FlagMenuItem_Tapped;
            smi.Tapped += FlagMenuItem_Tapped;
            bmi.Tapped += FlagMenuItem_Tapped;

            Menu menu = new Menu();
            menu.Items.Add(mmi);
            menu.Items.Add(pmi);
            menu.Items.Add(smi);
            menu.Items.Add(bmi);

            // Hide the webview and use the brush
            MainPage.Instance.ToggleWebView(false);

            Flyout flyout = new Flyout();
            flyout.PlacementTarget = FlagPostButton;
            flyout.Placement = PlacementMode.Bottom;
            flyout.Content = menu;
            flyout.IsOpen = true;
            flyout.Closed += (a, b) => { MainPage.Instance.ToggleWebView(true); };
        }

        private async void FlagMenuItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;

            if (mi != null && mi.CommandParameter is FlagCode)
            {
                FlagCode code = (FlagCode)mi.CommandParameter;
                string name = code.ToString();

                switch (code)
                {
                    case FlagCode.BestOf:
                        name = "Best of Craigslist";
                        break;
                    case FlagCode.Miscategorized:
                        name = "Miscategorized";
                        break;
                    case FlagCode.Prohibited:
                        name = "Prohibited";
                        break;
                    case FlagCode.Spam:
                        name = "Spam / Overpost";
                        break;
                    default:
                        await Logger.AssertNotReached("Unknown flag code");
                        break;
                }

                if (await Craigslist.Instance.FlagPost(this._post.Post, code))
                {
                    Utilities.ExecuteNotification("Craigslist 8X", string.Format("Successfully flagged post as: {0}", name));
                }
                else
                {
                    Utilities.ExecuteNotification("Craigslist 8X", string.Format("An error occurred trying to flag post as: {0}", name));
                }
            }
        }
        #endregion

        #region Full screen image
        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MainPage.Instance.ToggleWebView(show: false);

            List<BitmapImage> images = (from x in this._post.Pictures select new BitmapImage(x)).ToList();

            FlipView flipView = new FlipView();
            flipView.ItemsSource = images;
            flipView.SelectedItem = images[PicturesGrid.Items.IndexOf(new Uri((sender as FrameworkElement).DataContext as string))];
            flipView.ItemTemplate = this.Resources["FlipItemTemplate"] as DataTemplate;
            flipView.VerticalContentAlignment = Windows.UI.Xaml.VerticalAlignment.Center;
            flipView.MaxWidth = CoreApplication.MainView.CoreWindow.Bounds.Width;
            flipView.MaxHeight = CoreApplication.MainView.CoreWindow.Bounds.Height;
            flipView.Tapped += FlipView_Tapped;

            Canvas sp = new Canvas();
            sp.Width = CoreApplication.MainView.CoreWindow.Bounds.Width;
            sp.Height = CoreApplication.MainView.CoreWindow.Bounds.Height;
            sp.Background = new Windows.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color() { A = 225, R = 0, G = 0, B = 0 });
            sp.Children.Add(flipView);

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
                popup.IsOpen = false;
                MainPage.Instance.ToggleWebView(show: true);
            };
        }

        private void FlipView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse 
                && (e.OriginalSource is Border) 
                && (e.OriginalSource as Border).Child is Windows.UI.Xaml.Shapes.Path;
        }

        private void FlipViewImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }
        #endregion

        #region Mouse / Touch Handling
        private void PostTextView_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            this._verticalOffset = PostScroller.VerticalOffset;
        }

        private void PostTextView_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // Calculate 27mm worth of pixels.
            float pixels = Windows.Graphics.Display.DisplayProperties.LogicalDpi * 2.54f * 0.27f;
            e.Handled = true;

            if (this._translating != TranslateAxis.Y && (this._manipulating || Math.Abs(e.Cumulative.Translation.X) >= pixels))
            {
                this._manipulating = true;
                this._translating = TranslateAxis.X;
                MainPage.Instance.PanelView.PanelView_ManipulationDelta(sender, e);
            }
            else if (this._translating != TranslateAxis.X && Math.Abs(e.Cumulative.Translation.Y) >= pixels)
            {
                this.PostScroller.ScrollToVerticalOffset(_verticalOffset - e.Cumulative.Translation.Y);
                this._translating = TranslateAxis.Y;
            }
        }

        private void PostTextView_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            this._manipulating = false;
            this._translating = TranslateAxis.None;
        }

        private void PostTextView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (delta > 0)
            {
                if (PostScroller.VerticalOffset < 1)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
            else if (delta < 0) 
            {
                if (PostScroller.VerticalOffset >= PostScroller.ScrollableHeight)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
        }
        #endregion

        #region Nav Bar
        private void ShowPostNavBarButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.TogglePostNavBar(show: true);
        }

        private void HidePostNavBarButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.TogglePostNavBar(show: false);
        }

        private void TogglePostNavBar(bool show)
        {
            if (show)
            {
                Settings.Instance.ShowPostNavBar = true;
                PostNavBarGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
                ShowPostNavBarButton.Visibility = Visibility.Collapsed;
                HidePostNavBarButton.Visibility = Visibility.Visible;
            }
            else
            {
                Settings.Instance.ShowPostNavBar = false;
                PostNavBarGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                ShowPostNavBarButton.Visibility = Visibility.Visible;
                HidePostNavBarButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void ReloadPostButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this._post.DetailStatus = Post.PostDetailStatus.NotLoaded;
            await this.LoadPost();
        }

        private async void PreviousPostButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Logger.AssertNotNull(this._parentList, "This button should not be visible if there is no parentlist");
            if (this._parentList == null)
                return;

            if (this._parentList.PostItems == null)
                return;

            int index = this._parentList.PostItems.IndexOf(this._post);
            while (index > 0)
            {
                --index;
                if (this._parentList.PostItems[index] is PostVM)
                {
                    this._parentList.SetSelection(this._parentList.PostItems[index] as PostVM);
                    await this.ShowPost(this._parentList.PostItems[index] as PostVM);
                    MainPage.Instance.RemoveChildPanels(this);
                    return;
                }
            }

            Utilities.ExecuteNotification("Craigslist 8X", "You have reached the beginning of the list.");
        }

        private async void NextPostButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Logger.AssertNotNull(this._parentList, "This button should not be visible if there is no parentlist");
            if (this._parentList == null)
                return;

            if (this._parentList.PostItems == null)
                return;

            int index = this._parentList.PostItems.IndexOf(this._post);
            while (index < this._parentList.PostItems.Count - 1)
            {
                ++index;
                if (this._parentList.PostItems[index] is PostVM)
                {
                    this._parentList.SetSelection(this._parentList.PostItems[index] as PostVM);
                    await this.ShowPost(this._parentList.PostItems[index] as PostVM);
                    MainPage.Instance.RemoveChildPanels(this);
                    return;
                }
            }

            Utilities.ExecuteNotification("Craigslist 8X", "You have reached the end of the list.");
        }
        #endregion

        PostVM _post;
        bool _manipulating;
        double _verticalOffset;
        TranslateAxis _translating;
        IPostList _parentList;

        enum TranslateAxis
        {
            None,
            X,
            Y,
        }

        const string OneNotePostContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en-us"">
  <head>
    <title>{0}</title>
    <meta name=""created"" content=""{1}""/>
  </head>
  <body>
    From &lt;<a href=""{2}"">{2}</a>&gt;
    <br/>
    <img  data-render-src=""{2}"" alt=""Craigslist Post"" />
  </body>
<html>";
    }
}