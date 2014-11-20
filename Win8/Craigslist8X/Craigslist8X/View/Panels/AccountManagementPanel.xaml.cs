using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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
using Windows.UI.Popups;
using Windows.Security.Credentials;
using Windows.System;

using WB.CraigslistApi;
using WB.SDK;
using WB.SDK.Logging;

using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

using HtmlAgilityPack;
using WinRTXamlToolkit.Net;

namespace WB.Craigslist8X.View
{
    public sealed partial class AccountManagementPanel : UserControl, IPanel
    {
        public AccountManagementPanel()
        {
            this.InitializeComponent();
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            this._account = context as PasswordCredential;
            this._parent = parent as UIElement;
            this._handler = new HttpClientHandler();
            this._handler.UseCookies = true;
            this._handler.CookieContainer = new CookieContainer();
            this._client = new HttpClient(this._handler, disposeHandler: true);

            this.LoadingProgress.Visibility = Visibility.Visible;

            this.PageTitle.Text = string.Format("home of {0}", this._account.UserName);

            if (!WebHelper.IsConnectedToInternet())
            {
                await this.AbortAccountManagement("Could not detect network connectivity. Aborting account management.");
                return;
            }

            // If we are using cached creds, we need to login first.
            if (this._account != null)
            {
                this._account.RetrievePassword();

                // User may have changed his password or something and invalidated his cached credentials.
                if (!await Craigslist.Instance.Login(this._handler, this._client, this._account.UserName, this._account.Password))
                {
                    await this.AbortAccountManagement(string.Format("Failed to login with '{0}'. Please verify your cached credentials. Aborting account management.", this._account.UserName));
                    return;
                }
            }

            await this.ShowPostings();
        }

        public async Task RefreshPosts()
        {
            await ShowPostings();
        }

        private async Task ShowPostings()
        {
            this.LoadingProgress.Visibility = Visibility.Visible;

            // Initiate the posting
            HttpResponseMessage response = null;
            try
            {
                response = await this._client.GetAsync(ShowPostingsUri);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                await this.AbortAccountManagement("Received bad response from craigslist.");
                return;
            }
            else
            {
                await this.ParseResponse(response, false);
            }
        }

        private async Task ParseResponse(HttpResponseMessage response, bool wentBack)
        {
            bool success = false;

            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await response.Content.ReadAsStringAsync());

                var posts = (from table in doc.DocumentNode.Descendants("table").Where(x => x.Attributes["summary"] != null && x.Attributes["summary"].Value == "postings")
                             from rows in table.Descendants("tr").Where(x => x.Descendants("td").Any() && x.ParentNode == table)
                             select rows);

                List<PostingVM> postings = new List<PostingVM>();

                foreach (var post in posts)
                {
                    var posting = new PostingVM();

                    var status = (from td in post.Descendants("td").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "status") select td).First();
                    posting.Status = (PostingStatus)Enum.Parse(typeof(PostingStatus), Utilities.HtmlToText(status.InnerText).Trim());

                    var title = (from td in post.Descendants("td").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "title") select td).First();
                    Regex titleRegex = new Regex(@"\s*(.*?)\s*-\s*(\$\d+)?\s*$");
                    Match m = titleRegex.Match(title.InnerText.Replace("\n", string.Empty).Trim());
                    posting.Title = m.Groups[1].Value.Trim();
                    if (m.Groups.Count == 3)
                    {
                        posting.Price = m.Groups[2].Value.Trim();
                    }

                    var small = (from td in post.Descendants("td").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "areacat")
                                 from sm in td.Descendants("small")
                                 select sm).First();

                    var area = (from b in small.Descendants("b")
                                select b).First();
                    posting.City = area.InnerText.Trim();

                    area.Remove();
                    posting.Category = Utilities.HtmlToText(small.InnerText).Trim();

                    Regex dateRegex = new Regex(@".*(\d{4}-\d{2}-\d{2} \d{2}:\d{2})$");
                    var dates = (from td in post.Descendants("td").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "dates") select td).First();
                    posting.Date = DateTime.Parse(dateRegex.Match(dates.InnerText.Trim()).Groups[1].Value);

                    var postingId = (from td in post.Descendants("td").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "postingID") select td).First();
                    posting.PostingId = long.Parse(postingId.InnerText.Trim());
                    posting.Manage = new Uri(string.Format("https://post.craigslist.org/manage/{0}", posting.PostingId));

                    postings.Add(posting);
                }

                PostingsGrid.ItemsSource = postings;

                success = true;
            }
            catch (PostParseException ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogException(ex);
            }
            finally
            {
                this.LoadingProgress.Visibility = Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog(@"Sorry, we ran into an issue parsing the response from craigslist. Please notify the developer of this app so the issue may be investigated. 

In the mean time, would you like to manage your account in the browser?");
                dlg.Commands.Clear();
                dlg.Commands.Add(new UICommand("Yes", null, true));
                dlg.Commands.Add(new UICommand("No", null, false));

                UICommand result = await dlg.ShowAsync() as UICommand;
                if ((bool)result.Id)
                {
                    await Launcher.LaunchUriAsync(response.RequestMessage.RequestUri);
                }
            }
        }

        private async Task AbortAccountManagement(string message)
        {
            this.LoadingProgress.Visibility = Visibility.Collapsed;
            await new MessageDialog(message, "Craigslist 8X").ShowAsync();
            MainPage.Instance.RemoveChildPanels(this._parent);
            MainPage.Instance.PanelView.SnapToPanel(this._parent);
        }

        private async void Refresh_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await this.ShowPostings();
        }

        private async void OpenInBrowser_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(ShowPostingsUri);
        }

        private async void Posting_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var el = sender as FrameworkElement;
            if (el == null)
                return;

            var posting = el.DataContext as PostingVM;
            if (posting == null)
                return;

            var context = new ManagePostContext()
                {
                    Posting = posting,
                    Handler = this._handler,
                    Client = this._client,
                    Account = this._account
                };

            await MainPage.Instance.ExecuteManagePost(this, context);
        }

        PasswordCredential _account;
        HttpClient _client;
        HttpClientHandler _handler;
        UIElement _parent;

        readonly Uri ShowPostingsUri = new Uri("https://accounts.craigslist.org/?show_tab=postings");
    }
}
