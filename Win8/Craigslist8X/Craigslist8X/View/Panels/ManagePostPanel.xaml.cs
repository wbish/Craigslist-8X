using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace WB.Craigslist8X.View
{
    public sealed partial class ManagePostPanel : UserControl, IPanel
    {
        public ManagePostPanel()
        {
            this.InitializeComponent();
        }

        public async Task AttachContext(object context, IPanel parent)
        {
            var postingContext = context as ManagePostContext;
            if (postingContext == null)
            {
                await Logger.AssertNotReached("null managepostcontext?");
                await AbortAccountManagement("Invalid posting context. Aborting post management.");
                return;
            }

            _parent = parent as UIElement;
            _context = postingContext;
            _commands = new Dictionary<ManageCommand, Dictionary<string, string>>();

            HttpResponseMessage response = null;
            try
            {
                response = await this._context.Client.GetAsync(this._context.Posting.Manage);
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
                await this.ParseResponse(response);
            }
        }

        public async Task ParseResponse(HttpResponseMessage response)
        {
            bool success = false;

            this.TitleHeader.Text = string.Format("Manage Posting: {0}", this._context.Posting.Title);

            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await response.Content.ReadAsStringAsync());

                var container = (from pc in doc.DocumentNode.Descendants("div").Where(x => x.Id == "pagecontainer") select pc).FirstOrDefault();

                if (container != null)
                {
                    string html = @"<html>
<head>
<link type=""text/css"" rel=""stylesheet"" media=""all"" href=""https://post.craigslist.org/styles/clnew.css"">
</head>
<body class=""post"">
<article id=""pagecontainer"">
<section class=""body"">
{0}
</section>
</article>
</body>
</html>";
                    PostingWebView.NavigateToString(string.Format(html, container.OuterHtml));

                    EditButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    RepostButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    DeleteButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    UndeleteButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    _commands.Clear();

                    var edit = (from manageButtons in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "managebutton") 
                                from hidden in manageButtons.Descendants("input").Where(x => x.Attributes["type"].Value == "hidden" && x.Attributes["value"] != null && x.Attributes["value"].Value == "edit")
                                select hidden).FirstOrDefault();
                    if (edit != null)
                    {
                        _commands.Add(ManageCommand.Edit, new Dictionary<string, string>());

                        var fields = (from f in edit.ParentNode.Descendants("input") select f);
                        foreach (var field in fields)
                        {
                            _commands[ManageCommand.Edit].Add(field.Attributes["name"].Value, field.Attributes["value"].Value);
                        }
                        EditButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    var delete = (from manageButtons in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "managebutton")
                                  from hidden in manageButtons.Descendants("input").Where(x => x.Attributes["type"].Value == "hidden" && x.Attributes["value"] != null && x.Attributes["value"].Value == "delete")
                                select hidden).FirstOrDefault();
                    if (delete != null)
                    {
                        _commands.Add(ManageCommand.Delete, new Dictionary<string, string>());

                        var fields = (from f in delete.ParentNode.Descendants("input") select f);
                        foreach (var field in fields)
                        {
                            _commands[ManageCommand.Delete].Add(field.Attributes["name"].Value, field.Attributes["value"].Value);
                        }
                        DeleteButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    var repost = (from manageButtons in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "managebutton")
                                  from hidden in manageButtons.Descendants("input").Where(x => x.Attributes["type"].Value == "hidden" && x.Attributes["value"] != null && x.Attributes["value"].Value == "repost")
                                  select hidden).FirstOrDefault();
                    if (repost != null)
                    {
                        _commands.Add(ManageCommand.Repost, new Dictionary<string, string>());

                        var fields = (from f in repost.ParentNode.Descendants("input") select f);
                        foreach (var field in fields)
                        {
                            _commands[ManageCommand.Repost].Add(field.Attributes["name"].Value, field.Attributes["value"].Value);
                        }
                        RepostButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    var undelete = (from manageButtons in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "managebutton")
                                  from hidden in manageButtons.Descendants("input").Where(x => x.Attributes["type"].Value == "hidden" && x.Attributes["value"] != null && x.Attributes["value"].Value == "undelete")
                                  select hidden).FirstOrDefault();
                    if (undelete != null)
                    {
                        _commands.Add(ManageCommand.Undelete, new Dictionary<string, string>());

                        var fields = (from f in undelete.ParentNode.Descendants("input") select f);
                        foreach (var field in fields)
                        {
                            _commands[ManageCommand.Undelete].Add(field.Attributes["name"].Value, field.Attributes["value"].Value);
                        }
                        UndeleteButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    success = true;
                }
            }
            finally
            {
                this.ProgressBar.Visibility = Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog(@"Sorry, we ran into an issue parsing the response from craigslist. Please notify the developer of this app so the issue may be investigated. 

In the mean time, would you like to manage your post in the browser?");
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
            this.ProgressBar.Visibility = Visibility.Collapsed;
            await new MessageDialog(message, "Craigslist 8X").ShowAsync();
            MainPage.Instance.RemoveChildPanels(this._parent);
            MainPage.Instance.PanelView.SnapToPanel(this._parent);
        }

        private async void DeletePosting_Tapped(object sender, TappedRoutedEventArgs e)
        {
            bool success = false;

            try
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Visible;

                var query = GetPostData(ManageCommand.Delete);
                var uri = new Uri(string.Format("{0}?{1}", _context.Posting.Manage, query));
                HttpResponseMessage response = await _context.Client.GetAsync(uri);

                if (response.IsSuccessStatusCode)
                {
                    success = true;
                    var dlg = new MessageDialog("Are you sure you want to delete this post?", "Craigslist 8X");
                    dlg.Commands.Add(new UICommand("Yes"));
                    dlg.Commands.Add(new UICommand("No"));

                    var answer = await dlg.ShowAsync();

                    if (answer.Label == "Yes")
                    {
                        // Reset the success to false in case actual deleting fails and not because user anwered no
                        success = false;

                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(await response.Content.ReadAsStringAsync());

                        var delete = (from manageButtons in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "managebutton")
                                      from hidden in manageButtons.Descendants("input").Where(x => x.Attributes["type"].Value == "hidden" && x.Attributes["value"] != null && x.Attributes["value"].Value == "delete")
                                      select hidden).FirstOrDefault();
                        if (delete != null)
                        {
                            var form = delete.ParentNode;
                            var inputs = (from i in form.Descendants("input") select i);
                            string postData = string.Empty;
                            foreach (var input in inputs)
                            {
                                postData += string.Format("&{0}={1}", Uri.EscapeDataString(input.Attributes["name"].Value), Uri.EscapeDataString(input.Attributes["value"].Value));
                            }
                            postData = postData.Substring(1);

                            var content = new StringContent(postData);
                            content.Headers.Clear();
                            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                            response = await _context.Client.PostAsync(_context.Posting.Manage, content);

                            if (response.IsSuccessStatusCode)
                            {
                                if (this._parent is AccountManagementPanel)
                                {
                                    var managePosts = this._parent as AccountManagementPanel;
                                    managePosts.RefreshPosts();
                                }

                                MainPage.Instance.RemoveChildPanels(this._parent);
                                MainPage.Instance.PanelView.SnapToPanel(this._parent);
                                success = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            finally
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog("Sorry, we ran into an issue while trying to undelete this posting. Please try again.", "Craigslist 8X");
                await dlg.ShowAsync();
            }
        }

        private async void UndeletePosting_Tapped(object sender, TappedRoutedEventArgs e)
        {
            bool success = false;

            try
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Visible;

                var content = new StringContent(GetPostData(ManageCommand.Undelete));
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                HttpResponseMessage response = await _context.Client.PostAsync(_context.Posting.Manage, content);

                if (response.IsSuccessStatusCode)
                {
                    await ParseResponse(response);
                    success = true;

                    if (this._parent is AccountManagementPanel)
                    {
                        var managePosts = this._parent as AccountManagementPanel;
                        managePosts.RefreshPosts();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            finally
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog("Sorry, we ran into an issue while trying to undelete this posting. Please try again.", "Craigslist 8X");
                await dlg.ShowAsync();
            }
        }

        private async void RepostPosting_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // For some fucked up reason, much like everything on Craigslist, REPOST is different, 
            // all other actions. We need to do a GET instead of a POST
            CreatePostContext ctx = new CreatePostContext();
            ctx.Handler = _context.Handler;
            ctx.Client = _context.Client;
            ctx.Account = _context.Account;
            ctx.Url = GetRepostUri();

            MainPage.Instance.RemoveChildPanels(this._parent);
            await MainPage.Instance.ExecuteCreatePost(this._parent, ctx);
        }

        private async void EditPosting_Tapped(object sender, TappedRoutedEventArgs e)
        {
            bool success = false;

            try
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Visible;

                var content = new StringContent(GetPostData(ManageCommand.Edit));
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                HttpResponseMessage response = await _context.Client.PostAsync(_context.Posting.Manage, content);

                if (response.IsSuccessStatusCode)
                {
                    CreatePostContext ctx = new CreatePostContext();
                    ctx.Handler = _context.Handler;
                    ctx.Client = _context.Client;
                    ctx.Account = _context.Account;
                    ctx.Url = response.RequestMessage.RequestUri;

                    MainPage.Instance.RemoveChildPanels(this._parent);
                    await MainPage.Instance.ExecuteCreatePost(this._parent, ctx);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            finally
            {
                ProgressBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog("Sorry, we ran into an issue while trying to edit this posting. Please try again.", "Craigslist 8X");
                await dlg.ShowAsync();
            }
        }

        private Uri GetRepostUri()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_context.Posting.Manage.ToString());

            for (int i = 0; i < this._commands[ManageCommand.Repost].Count; ++i)
            {
                sb.Append(i == 0 ? "?" : "&");
                var kvp = this._commands[ManageCommand.Repost].ElementAt(i);

                sb.AppendFormat("{0}={1}", Uri.EscapeUriString(kvp.Key), kvp.Value.Replace(' ', '+'));
            }

            return new Uri(sb.ToString());
        }

        private string GetPostData(ManageCommand cmd)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < this._commands[cmd].Count; ++i)
            {
                sb.Append(i == 0 ? string.Empty : "&");
                var kvp = this._commands[cmd].ElementAt(i);

                sb.AppendFormat("{0}={1}", Uri.EscapeDataString(kvp.Key), Uri.EscapeDataString(kvp.Value));
            }

            return sb.ToString();
        }

        UIElement _parent;
        ManagePostContext _context;
        Dictionary<ManageCommand, Dictionary<string, string>> _commands;
    }

    enum ManageCommand
    {
        Edit,
        Delete,
        Repost,
        Undelete,
    }

    public class ManagePostContext
    {
        public PasswordCredential Account
        {
            get;
            set;
        }

        public HttpClientHandler Handler
        {
            get;
            set;
        }

        public HttpClient Client
        {
            get;
            set;
        }

        public PostingVM Posting
        {
            get;
            set;
        }
    }
}