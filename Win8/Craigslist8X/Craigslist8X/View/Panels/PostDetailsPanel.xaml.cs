using System;
using System.Threading.Tasks;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

using Bing.Maps;
using Callisto.Controls;

using WB.Craigslist8X.GeocodeService;
using WB.Craigslist8X.RouteService;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class PostDetailsPanel : UserControl, IPanel
    {
        public PostDetailsPanel()
        {
            this.InitializeComponent();

            this._geoClient = new GeocodeServiceClient(GeocodeServiceClient.EndpointConfiguration.BasicHttpBinding_IGeocodeService);
            this._routeClient = new RouteServiceClient(RouteServiceClient.EndpointConfiguration.BasicHttpBinding_IRouteService);
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            PostVM post = context as PostVM; 
            this._post = post;
            this.DataContext = this._post;

            // Hide unused fields
            if (string.IsNullOrEmpty(this._post.Email))
            {
                EmailLabel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                EmailLink.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(this._post.Phone))
            {
                PhoneLabel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                PhoneLink.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(this._post.Price))
            {
                PriceLabel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Price.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(this._post.Location))
            {
                LocationLabel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Location.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(this._post.Timestamp))
            {
                DateLabel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                Date.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            // Initialize map data
            if (Craigslist8XData.Location == null)
            {
                Geolocator locator = new Geolocator();
                locator.DesiredAccuracy = PositionAccuracy.Default;
                Craigslist8XData.Location = await locator.GetGeopositionAsync();
            }

            Bing.Maps.Location currentLocation = null;
            if (Craigslist8XData.Location != null)
            {
                currentLocation = new Bing.Maps.Location(Craigslist8XData.Location.Coordinate.Latitude, Craigslist8XData.Location.Coordinate.Longitude);
                this.AddPushpin("A", currentLocation);
            }

            Bing.Maps.Location postLocation = null;

            if (this._post.HasMap)
            {
                postLocation = new Bing.Maps.Location(this._post.Post.MapCoordinate.Latitude, this._post.Post.MapCoordinate.Longitude);
            }
            else
            {
                postLocation = await GetLocationPoint(this._post.Location);
            }

            if (postLocation != null)
            {
                this.AddPushpin("B", postLocation);
            }

            if (currentLocation != null && postLocation != null)
            {
                RouteService.RouteResult result = await GetRouteResult(currentLocation, postLocation);
                if (result != null)
                {
                    this.SetRoute(result);
                }
            }
        }

        #region Maps
        private void AddPushpin(string label, Bing.Maps.Location loc)
        {
            Pushpin pp = new Pushpin() { Text = label };
            MapLayer.SetPosition(pp, loc);
            this.PostMap.Children.Add(pp);
            this.PostMap.Center = loc;
        }

        private void SetRoute(RouteService.RouteResult route)
        {
            // Trying to map too many points will hang the application
            if (!route.RoutePath.Points.Any() || route.RoutePath.Points.Count > 1000)
            {
                return;
            }

            Bing.Maps.MapPolyline routeLine = new MapPolyline();
            routeLine.Color = Windows.UI.Colors.Blue;
            routeLine.Locations = new LocationCollection();
            routeLine.Width = 5;

            foreach (RouteService.Location loc in route.RoutePath.Points)
            {
                routeLine.Locations.Add(new Bing.Maps.Location(loc.Latitude, loc.Longitude));
            }

            this.PostMap.ShapeLayers.Clear();
            MapShapeLayer layer = new MapShapeLayer();
            this.PostMap.ShapeLayers.Add(layer);
            layer.Shapes.Clear();
            layer.Shapes.Add(routeLine);

            //Set the map view
            LocationRect rect = new LocationRect(routeLine.Locations);
            this.PostMap.SetView(rect);
        }

        private async Task<Bing.Maps.Location> GetLocationPoint(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            GeocodeRequest request = new GeocodeRequest();
            request.ExecutionOptions = new GeocodeService.ExecutionOptions();
            request.ExecutionOptions.SuppressFaults = true;
            request.Query = address;
            request.Credentials = new GeocodeService.Credentials() { Token = BingToken };

            GeocodeResponse response = await this._geoClient.GeocodeAsync(request);

            if (response.Results.Count() > 0)
            {
                return new Bing.Maps.Location(response.Results[0].Locations[0].Latitude, response.Results[0].Locations[0].Longitude);
            }

            return null;
        }

        private async Task<RouteService.RouteResult> GetRouteResult(Bing.Maps.Location a, Bing.Maps.Location b)
        {
            if (a == null || b == null)
                return null;

            RouteRequest request = new RouteRequest();
            request.ExecutionOptions = new RouteService.ExecutionOptions();
            request.ExecutionOptions.SuppressFaults = true;
            request.Options = new RouteOptions();
            request.Options.RoutePathType = RoutePathType.Points;
            request.Options.TrafficUsage = TrafficUsage.None;
            request.Options.Mode = TravelMode.Driving;
            request.Credentials = new RouteService.Credentials() { Token = BingToken };

            request.Waypoints = new System.Collections.ObjectModel.ObservableCollection<Waypoint>();
            request.Waypoints.Add(new Waypoint() { Description = "Current Location", Location = new RouteService.Location() { Latitude = a.Latitude, Longitude = a.Longitude } });
            request.Waypoints.Add(new Waypoint() { Description = "Item Location", Location = new RouteService.Location() { Latitude = b.Latitude, Longitude = b.Longitude } });

            RouteResponse response = await this._routeClient.CalculateRouteAsync(request);

            return response.Result;
        }
        #endregion

        private async void UrlLink_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (this._post.Post.Url == null)
            {
                await Logger.AssertNotReached("Url is null? WTF?");
                return;
            }

            var menu = new Menu();
            var copy = new MenuItem { Text = "Copy" };
            var browse = new MenuItem { Text = "Browse Link" };
            copy.Tapped += (a, b) => { DataPackage pkg = new DataPackage(); pkg.SetText(this._post.Url); Clipboard.SetContent(pkg); };
            browse.Tapped += async (a, b) => { await Windows.System.Launcher.LaunchUriAsync(new Uri(this._post.Url)); };
            menu.Items.Add(copy);
            menu.Items.Add(browse);

            this.ShowMenu(sender as UIElement, menu);
        }

        private async void EmailLink_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this._post.Post.Email))
            {
                await Logger.AssertNotReached("Email is null? WTF?");
                return;
            }
            var menu = new Menu();
            var copy = new MenuItem { Text = "Copy" };
            var send = new MenuItem { Text = "Send Email" };
            copy.Tapped += (a, b) => { DataPackage pkg = new DataPackage(); pkg.SetText(this._post.Email); Clipboard.SetContent(pkg); };
            send.Tapped += async (a, b) => { await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format("mailto:?to={0}&subject={1}&body={2}", this._post.Email, this._post.Title, this._post.Post.Url.ToString()))); };
            menu.Items.Add(copy);
            menu.Items.Add(send);

            this.ShowMenu(sender as UIElement, menu);
        }

        private void PhoneLink_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this._post.Post.Phone))
                return;

            var menu = new Menu();
            var copy = new MenuItem { Text = "Copy" };
            var call = new MenuItem { Text = "Call Phone" };
            copy.Tapped += (a, b) => { DataPackage pkg = new DataPackage(); pkg.SetText(this._post.Phone); Clipboard.SetContent(pkg); };
            call.Tapped += async (a, b) => { await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format("callto:+1{0}", this._post.Phone))); };
            menu.Items.Add(copy);
            menu.Items.Add(call);

            this.ShowMenu(sender as UIElement, menu);
        }

        private void ShowMenu(UIElement context, Menu menu)
        {
            // Show the menu in a flyout anchored to the header title
            var flyout = new Flyout();
            flyout.Placement = PlacementMode.Bottom;
            flyout.HorizontalAlignment = HorizontalAlignment.Left;
            flyout.HorizontalContentAlignment = HorizontalAlignment.Left;
            flyout.PlacementTarget = context;
            flyout.Content = menu;
            flyout.IsOpen = true;
        }

        PostVM _post;
        GeocodeServiceClient _geoClient;
        RouteServiceClient _routeClient;
    
        const string BingToken = "Al8PeMQARMlRy4ks02r-xP3EOfrHj5xmExwdIzIhQezYnseuvXlXGN-t9Sy-OZHr";
    }
}