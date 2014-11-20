using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Store;
using Windows.Data.Xml.Dom;
using Windows.UI.ApplicationSettings;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

using Newtonsoft.Json;

using WinRTXamlToolkit.Tools;

using WB.SDK.Logging;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.View;

namespace WB.Craigslist8X
{
    partial class App
    {
        public App()
        {
            InitializeComponent();

            this.Resuming += AppResuming;
            this.Suspending += OnSuspending;
            this.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
            DebugSettings.IsBindingTracingEnabled = false;

            this.RequestedTheme = ApplicationTheme.Light;
        }

        async void OnResuming(object sender, object e)
        {
            await SavedSearches.Instance.LoadNotificationsAsync();
        }

        private async void BackgroundTimer_Tick(object sender, object e)
        {
            using (new MonitoredScope("Craigslist8X Background Tasks"))
            {
                await Craigslist8XData.SaveAsync();
                await SavedSearches.Instance.LoadNotificationsAsync();
            }
        }

        private async Task InitCraigslist8X()
        {
            using (new MonitoredScope("Initialize Craigslist8X"))
            {
#if DEBUG
                LicenseInfo = CurrentAppSimulator.LicenseInformation;
#else
                LicenseInfo = CurrentApp.LicenseInformation;
#endif

                bool loaded = await Craigslist8XData.LoadAsync();

                // Load notification data every time we start the app because background task may have kicked in
                await SavedSearches.Instance.LoadNotificationsAsync();

                if (loaded)
                {
                    // Hook up settings handlers
                    SettingsPane.GetForCurrentView().CommandsRequested += SettingsUI.SettingsUI_CommandsRequested;

                    if (this.BackgroundTimer == null)
                    {
                        this.BackgroundTimer = new BackgroundTimer();
                        this.BackgroundTimer.Interval = TimeSpan.FromSeconds(15);
                        this.BackgroundTimer.Tick += BackgroundTimer_Tick;
                        this.BackgroundTimer.IsEnabled = true;
                        this.BackgroundTimer.Start();
                    }
                }

                // Ensure we have a window to look at
                if (MainPage.Instance == null)
                {
                    Frame root = await EnsureNavigationFrame();
                    root.Navigate(typeof(MainPage), null);

                    DataTransferManager.GetForCurrentView().DataRequested += MainPage.Instance.MainPage_DataRequested;
                }
            }
        }

        protected async override void OnLaunched(LaunchActivatedEventArgs args)
        {
            using (new MonitoredScope("Launch Craigslist8X"))
            {
                await this.InitCraigslist8X();

                try
                {
                    if (!string.IsNullOrEmpty(args.Arguments))
                    {
                        try
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(args.Arguments);
                            XmlElement xe = doc.DocumentElement;

                            if (xe.NodeName == "SavedQuery")
                            {
                                await MainPage.Instance.ExecuteSavedSearches(null, Uri.UnescapeDataString(xe.GetAttribute("Name")));
                            }
                        }
                        catch
                        {
                        }

                        // It could have been a JSON argument instead of XML
                        dynamic argObject = JsonConvert.DeserializeObject(args.Arguments);
                        if (argObject.type == "toast")
                        {
                            var webUrl = argObject.clientUrl;
                            var uri = new Uri(webUrl.ToString());
                            await Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogMessage("Exception thrown launching app with the following arguments: {0}", args.Arguments);
                    Logger.LogException(ex);
                }
            }
        }

        protected async override void OnSearchActivated(SearchActivatedEventArgs args)
        {
            using (new MonitoredScope("Search Activate Craigslist8X"))
            {
                await this.InitCraigslist8X();

                if (!CityManager.Instance.SearchCitiesDefined)
                {
                    MessageDialog dlg = new MessageDialog("Please set the city you want to search and try again.", "Craigslist 8X");
                    await dlg.ShowAsync();
                    SettingsUI.ShowSearchSettings();
                }
                else
                {
                    // As of this writing, this app has two actual pages. The MainPage and the ChooceCitiesPage. We need to ensure that 
                    // we navigate to the MainPage.
                    Frame frame = await EnsureNavigationFrame();
                    frame.Content = MainPage.Instance;

                    Query template = new Query(CityManager.Instance.SearchCities.First(), CategoryManager.Instance.SearchCategory, args.QueryText);
                    template.HasImage = Settings.Instance.OnlyShowPostsPictures;
                    template.Type = Settings.Instance.OnlySearchTitles ? Query.QueryType.TitleOnly : Query.QueryType.EntirePost;

                    QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);

                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
                        async () =>
                        {
                            await MainPage.Instance.ExecuteSearchQuery(null, qb);
                        });
                }
            }
        }

        protected async void AppResuming(object sender, object e)
        {
            await SavedSearches.Instance.LoadNotificationsAsync();
        }

        protected async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            using (new MonitoredScope("Suspend Craigslist8X"))
            {
                SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
                await Craigslist8XData.SaveAsync();
                deferral.Complete();
            }
        }

        protected async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.LogException(e.Exception);

            await Craigslist8XData.SaveAsync();
        }

        #region Static
        public static async Task<Frame> EnsureNavigationFrame()
        {
            if (Window.Current.Content == null)
            {
                Window.Current.Content = new Frame();
            }
            else if (!(Window.Current.Content is Frame))
            {
                await Logger.AssertNotReached("Why is Window.Current.Content set to a non null type that is not a Frame?");
                Window.Current.Content = new Frame();
            }

            Window.Current.Activate();

            return (Frame)Window.Current.Content;
        }

        public static LicenseInformation LicenseInfo;
        #endregion

        #region Pro
        internal const string Craigslist8XPRO = "Craigslist8XPRO";

        public static bool IsPro
        {
            get
            {
#if CRAIGSLIST8XPRO
                return true;
#else
                return LicenseInfo.ProductLicenses[Craigslist8XPRO].IsActive;
#endif
            }
        }
        #endregion

        private BackgroundTimer BackgroundTimer;
    }
}
