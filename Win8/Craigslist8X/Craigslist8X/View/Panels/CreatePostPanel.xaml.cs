using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Security.Credentials;

using HtmlAgilityPack;
using Syncfusion.UI.Xaml.Controls.Input;
using WinRTXamlToolkit.Controls;
using WinRTXamlToolkit.Tools;
using WinRTXamlToolkit.Net;

using WB.SDK;
using WB.SDK.Logging;
using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    public sealed partial class CreatePostPanel : UserControl, IPanel, IDisposable
    {
        public CreatePostPanel()
        {
            this.InitializeComponent();
        }

        #region IPanel
        public async Task AttachContext(object context, IPanel parent)
        {
            this._backHistory = new Stack<Uri>();
            this._context = (CreatePostContext)context;
            this._parent = parent as UIElement;

            if (this._context.Handler == null)
            {
                this._context.Handler = new HttpClientHandler();
                this._context.Handler.UseCookies = true;
                this._context.Handler.CookieContainer = new CookieContainer();
            }

            if (this._context.Client == null)
            {
                this._context.Client = new HttpClient(this._context.Handler, disposeHandler: true);
            }

            this.LoadingProgress.Visibility = Visibility.Visible;

            if (!WebHelper.IsConnectedToInternet())
            {
                await this.AbortPost("Could not detect network connectivity. Aborting create post.");
                return;
            }

            // If we are using cached creds, we need to login first.
            if (this._context.Account != null)
            {
                this._context.Account.RetrievePassword();

                // User may have changed his password or something and invalidated his cached credentials.
                if (!await Craigslist.Instance.Login(this._context.Handler, this._context.Client, this._context.Account.UserName, this._context.Account.Password))
                {
                    await this.AbortPost(string.Format("Failed to login with '{0}'. Please verify your cached credentials. Aborting create post.", this._context.Account.UserName));
                    return;
                }
            }

            // Get the city specific create post URL
            Uri startPostLink = null;

            if (_context.Url != null)
                startPostLink = _context.Url;
            else
                startPostLink = await Craigslist.Instance.GetPostUri(this._context.City);

            if (startPostLink == null)
            {
                await this.AbortPost("Unexpected error initiating new post session from craigslist.org. Aborting create post.");
                return;
            }

            // Initiate the posting
            HttpResponseMessage response = null;
            try
            {
                response = await this._context.Client.GetAsync(startPostLink);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                await this.AbortPost("Received bad response from craigslist.");
                return;
            }
            else
            {
                // Set the post link for followup requests
                this._postLink = new Uri(response.RequestMessage.RequestUri.AbsoluteUri.Replace(response.RequestMessage.RequestUri.Query, string.Empty));
                await this.ParseResponse(response, false);
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this._context.Client != null)
                {
                    this._context.Client.Dispose();
                    this._context.Client = null;
                }
            }
        }
        #endregion

        #region Response Parsing
        private async Task AbortPost(string message)
        {
            this.LoadingProgress.Visibility = Visibility.Collapsed;
            await new MessageDialog(message, "Craigslist 8X").ShowAsync();
            MainPage.Instance.RemoveChildPanels(this._parent);
            MainPage.Instance.PanelView.SnapToPanel(this._parent);
        }

        private async Task ParseResponse(HttpResponseMessage response, bool wentBack)
        {
            string query = response.RequestMessage.RequestUri.Query;
            bool success = false;

            try
            {
                HtmlNode.ElementsFlags.Remove("form");
                HtmlNode.ElementsFlags.Remove("option");

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(await response.Content.ReadAsStringAsync());

                this.SetTitle(doc);

                // Grab and cache the hidden value identifying this posting session
                var form = (from f in doc.DocumentNode.Descendants("form") select f).FirstOrDefault();
                if (form != null)
                {
                    var hiddens = (from input in form.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "hidden") select input);

                    if (hiddens != null)
                    {
                        this._hiddenFields = new List<FormHiddenField>();
                        foreach (var h in hiddens)
                        {
                            this._hiddenFields.Add(new FormHiddenField(h.Attributes["name"].Value, h.Attributes["value"].Value));
                        }
                    }
                }

                // Most of the create post pages are "pick one of the following" type pages. So we handle those in a generic container.
                if (query.Contains("s=type") || query.Contains("s=cat") || query.Contains("s=hcat") ||
                    query.Contains("s=ptype") || query.Contains("s=subarea") || query.Contains("s=ltr") ||
                    query.Contains("s=gs"))
                {
                    if (query.Contains("s=cat") && doc.DocumentNode.InnerText.IndexOf("select one or more categories", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        await this.ParseJobCategories(doc);
                    }
                    else
                    {
                        string label = "Choose one of the following";

                        if (query.Contains("s=type"))
                            label = "Choose your type of posting";
                        else if (query.Contains("s=hcat"))
                            label = "Choose your category";
                        else if (query.Contains("s=subarea"))
                            label = "Choose the area nearest you";
                        else if (query.Contains("s=ptype"))
                            label = "Choose what you have in mind";
                        else if (query.Contains("s=ltr"))
                            label = "Choose the kind of relationship you are looking for";
                        else if (query.Contains("s=cat"))
                            label = "Choose a category";
                        else if (query.Contains("s=gs"))
                            label = "Do you want to hire someone for a short-term gig or are you offering a service?";

                        await this.ParsePickOne(doc, label);
                    }
                }
                else if (query.Contains("s=mix"))
                {
                    await this.ParseMix(doc);
                }
                else if (query.Contains("s=editimage"))
                {
                    await this.ParseEditImages(doc);
                }
                else if (query.Contains("s=edit"))
                {
                    await this.ParseEdit(doc);
                }
                else if (query.Contains("s=account"))
                {
                    PasswordCredential account = await UserAccounts.PromptForCreds("Login to craigslist to continue with your posting");

                    if (account != null)
                    {
                        account.RetrievePassword();

                        if (!await Craigslist.Instance.Login(this._context.Handler, this._context.Client, account.UserName, account.Password))
                        {
                            await new MessageDialog("Login failed. Pease verify your username and password and try again.", "Craigslist 8X").ShowAsync();
                        }
                        else
                        {
                            await new MessageDialog("Login successful.", "Craigslist 8X").ShowAsync();
                            this._context.Account = account;
                        }
                    }

                    this.ToggleNavigationButtons(true);
                    this.LoadingProgress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    return;
                }
                else if (query.Contains("s=geoverify") || query.Contains("s=freejob"))
                {
                    var inputs = (from input in doc.DocumentNode.Descendants("input").Where(x => x.Attributes["name"] != null) select input);
                    StringBuilder sb = new StringBuilder();
                    foreach (var input in inputs)
                    {
                        sb.Append(string.Format("&{0}={1}", Uri.EscapeDataString(input.Attributes["name"].Value), Uri.EscapeDataString(input.Attributes["value"].Value)));
                    }

                    HttpContent content = new StringContent(sb.ToString().Substring(1));
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    this._context.Client.DefaultRequestHeaders.ExpectContinue = false;
                    await this.IssueRequest(content);
                    return;
                }
                else if (query.Contains("s=preview"))
                {
                    await this.ParseEditPreview(doc);
                }
                else if (query.Contains("s=mailoop") || query.Contains("s=redirect"))
                {
                    await this.ParseEmailApproval(doc);
                }
                else
                {
                    MessageDialog dlg = new MessageDialog(@"Sorry, we have reached an unexpected posting state. Please notify the developer of this app so the issue may be fixed. 

In the mean time, would you like to finish your post in the browser?");
                    dlg.Commands.Clear();
                    dlg.Commands.Add(new UICommand("Yes", null, true));
                    dlg.Commands.Add(new UICommand("No", null, false));

                    UICommand result = await dlg.ShowAsync() as UICommand;
                    if ((bool)result.Id)
                    {
                        await Launcher.LaunchUriAsync(response.RequestMessage.RequestUri);
                    }
                }

                // Once we have finished parsing the response and shown appropriate UI, we store the URI
                // so we can enable the back button.
                this._currentPage = response.RequestMessage.RequestUri;
                this.ToggleNavigationButtons(true);

                success = true;
            }
            catch (PostParseException ex)
            {
                Logger.LogException(ex);
            }
            finally
            {
                this.LoadingProgress.Visibility = Visibility.Collapsed;
            }

            if (!success)
            {
                MessageDialog dlg = new MessageDialog(@"Sorry, we ran into an issue parsing the response from craigslist. Please notify the developer of this app so the issue may be investigated. 

In the mean time, would you like to finish your post in the browser?");
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

        private async Task ParsePickOne(HtmlDocument doc, string label)
        {
            var formNode = (from body in doc.DocumentNode.Descendants("body")
                            from form in body.Descendants("form")
                            select form).FirstOrDefault();

            if (formNode == null)
            {
                throw new PostParseException("Could not find form node");
            }

            var radios = (from input in formNode.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "radio")
                          select input);
            if (!radios.Any())
            {
                throw new PostParseException("Could not find radio options.");
            }

            var items = (from input in radios
                         select new PickItemVM(input.ParentNode.InnerText.Trim(), input)).ToList();

            this.PickOneLabel.Text = label;
            this.PickOneGrid.ItemsSource = items;

            await this.FinalizeSetPostState(CreatePostState.PickOne);
        }

        private async Task ParseJobCategories(HtmlDocument doc)
        {
            var formNode = (from body in doc.DocumentNode.Descendants("body")
                            from form in body.Descendants("form")
                            select form).FirstOrDefault();

            if (formNode == null)
            {
                throw new PostParseException("Could not find form node");
            }

            var checkboxes = (from input in formNode.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "checkbox")
                              select input);

            if (!checkboxes.Any())
            {
                throw new PostParseException("Could not find checkbox inputs");
            }

            List<PickItemVM> items = (from input in checkboxes
                                      select new PickItemVM(input.ParentNode.InnerText.Trim(), input)).ToList();

            this.JobsCategoryGrid.ItemsSource = items;

            // Mark selected items
            foreach (var item in items)
            {
                if (item.Node.Attributes["checked"] != null)
                    this.JobsCategoryGrid.SelectedItems.Add(item);
            }

            await this.FinalizeSetPostState(CreatePostState.JobsCategory);
        }

        private async Task ParseMix(HtmlDocument doc)
        {
            var formNode = (from body in doc.DocumentNode.Descendants("body")
                            from form in body.Descendants("form")
                            select form).FirstOrDefault();

            if (formNode == null)
            {
                throw new PostParseException("Could not find form node");
            }

            var fieldSets = (from fieldSet in formNode.Descendants("fieldset")
                             select fieldSet).ToList();

            if (fieldSets.Count != 2)
            {
                throw new PostParseException("Could not find fieldset nodes");
            }

            LeftMixGrid.ItemsSource = (from radio in fieldSets.First().Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "radio")
                                       select new PickItemVM(
                                           (from label in radio.ParentNode.Descendants("label").Where(x => x.Attributes["for"] != null && x.Attributes["for"].Value == radio.Id) select label).First().InnerText,
                                           radio));

            RightMixGrid.ItemsSource = (from radio in fieldSets.Last().Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "radio")
                                        select new PickItemVM(
                                           (from label in radio.ParentNode.Descendants("label").Where(x => x.Attributes["for"] != null && x.Attributes["for"].Value == radio.Id) select label).First().InnerText,
                                           radio));

            await this.FinalizeSetPostState(CreatePostState.Mix);
        }

        private async Task ParseEditImages(HtmlDocument doc)
        {
            // We only want the continue form fields to show up here
            this._hiddenFields.Clear();

            // There are a minimum of 3 forms on this page, we need the right one. (the right one being the done form)
            var form = (from f in doc.DocumentNode.Descendants("form")
                        from d in f.Descendants("button").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("done"))
                        from fg in d.Ancestors("form")
                        select fg).FirstOrDefault();

            var hiddenGo = (from input in form.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "hidden") select input);
            foreach (var h in hiddenGo)
            {
                this._hiddenFields.Add(new FormHiddenField(h.Attributes["name"].Value, h.Attributes["value"].Value));
            }

            var maxImgCount = (from divposting in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("posting"))
                               from p in divposting.Descendants("p").Where(x => x.InnerText.Contains("add up to"))
                               select p).FirstOrDefault();
            if (maxImgCount != null)
            {
                ImageBoxText.Text = maxImgCount.InnerText.Trim();
            }

            var imgbox = (from ib in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "imgbox")
                          select ib);

            List<ImageBoxVM> images = new List<ImageBoxVM>();

            // Add the images already on the server
            foreach (var ib in imgbox)
            {
                ImageBoxVM vm = new ImageBoxVM();

                var hidden = (from input in ib.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "hidden")
                              select input);
                foreach (var h in hidden)
                {
                    vm.HiddenFields.Add(new FormHiddenField(h.Attributes["name"].Value, h.Attributes["value"].Value));
                }
                vm.Image = (from uri in ib.Descendants("img").Where(x => x.Attributes["src"] != null)
                            select new Uri(uri.Attributes["src"].Value)).First();

                images.Add(vm);
            }

            // Add the Add more items image box
            var addMoreDiv = (from add in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "addmore")
                              select add).First();
            if (addMoreDiv.Attributes["style"] == null || (addMoreDiv.Attributes["style"] != null && addMoreDiv.Attributes["style"].Value.Contains("none")))
            {
                AddImageBoxVM vm = new AddImageBoxVM();
                vm.Image = new Uri("ms-appx:///Resources/AddImage.png");

                var hidden = (from input in addMoreDiv.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "hidden")
                              select input);
                foreach (var h in hidden)
                {
                    vm.HiddenFields.Add(new FormHiddenField(h.Attributes["name"].Value, h.Attributes["value"].Value));
                }

                images.Add(vm);
            }

            ImageBoxGrid.ItemsSource = images;

            await this.FinalizeSetPostState(CreatePostState.EditImages);
        }

        private async Task ParseEdit(HtmlDocument doc)
        {
            // Clean up the posting fields. User may have navigated away and come back.
            List<UIElement> remove = new List<UIElement>();
            foreach (var el in this.PostingFields.Children)
            {
                if (el == this.PostingTitleText || el == this.SpecificLocText)
                    continue;
                remove.Add(el);
            }
            foreach (var el in remove)
            {
                this.PostingFields.Children.Remove(el);
            }

            var formNode = (from form in doc.DocumentNode.Descendants("form").Where(x => x.Id == "postingForm") select form).FirstOrDefault();
            if (formNode == null)
            {
                throw new PostParseException("Could not find form node");
            }

            await this.ParseEditTitleRow(formNode);

            await this.ParseEditEmailRow(formNode);

            await this.ParseEditEventDates(formNode);

            await this.ParseCompensation(formNode);

            this.ParseEditPostMap(formNode);

            this.ParseEditPerms(formNode);

            await this.FinalizeSetPostState(CreatePostState.Edit);
        }

        private async Task ParseEditPreview(HtmlDocument doc)
        {
            var post = (from posting in doc.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("posting")) select posting).FirstOrDefault();

            if (post != null)
            {
                PreviewWebView.NavigateToString(post.OuterHtml);
            }

            await this.FinalizeSetPostState(CreatePostState.Preview);
        }

        private async Task ParseEmailApproval(HtmlDocument doc)
        {
            var email = (from font in doc.DocumentNode.Descendants("font").Where(x => x.Attributes["color"] != null && x.Attributes["color"].Value == "green") select font).FirstOrDefault();

            if (email != null)
            {
                ApprovalEmail.Text = string.Format("Email sent to: {0}", email.InnerText);
            }

            await this.FinalizeSetPostState(CreatePostState.Approval);
        }

        private async Task ParseEditTitleRow(HtmlNode formNode)
        {
            var titleRow = (from div in formNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "title row") select div).FirstOrDefault();
            if (titleRow == null)
            {
                throw new PostParseException("Could not find title row");
            }

            // The titlerow should only contain divs, which in turn may or may not contain actual fields
            foreach (var node in titleRow.ChildNodes)
            {
                if (node.InnerText.IndexOf("Posting Title", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PostingTitleText.Tag = node.Descendants("input").First().Attributes["name"].Value;
                    PostingTitleText.Text = node.Descendants("input").First().Attributes["value"].Value;
                    continue;
                }

                if (node.InnerText.IndexOf("Specific Location", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    SpecificLocText.Tag = node.Descendants("input").First().Attributes["name"].Value;
                    SpecificLocText.Text = node.Descendants("input").First().Attributes["value"].Value;
                    continue;
                }

                var inputNode = (from input in node.Descendants("input") select input).FirstOrDefault();
                if (inputNode != null)
                {
                    WatermarkTextBox tb = new WatermarkTextBox();
                    tb.WatermarkText = node.InnerText.Replace(":", string.Empty).Replace("$", string.Empty).Trim();
                    tb.Tag = inputNode.Attributes["name"].Value;
                    tb.Margin = new Thickness(10, 5, 10, 5);
                    tb.Text = node.Descendants("input").First().Attributes["value"].Value;

                    if (tb.WatermarkText.Contains("Price") || tb.WatermarkText.Contains("Age"))
                    {
                        tb.InputScope = new InputScope();
                        tb.InputScope.Names.Add(new InputScopeName() { NameValue = InputScopeNameValue.NumberFullWidth });
                    }

                    if (inputNode.Attributes["class"] != null && inputNode.Attributes["class"].Value.Contains("req"))
                    {
                        tb.BorderBrush = new SolidColorBrush(Windows.UI.Colors.DarkGreen);
                    }

                    PostingFields.Children.Add(tb);
                    continue;
                }

                var missedNamedField = (from field in node.Descendants().Where(x => x.Attributes["name"] != null && x.Attributes["type"] != null && x.Attributes["type"].Value == "checkbox") select field);
                if (missedNamedField != null && missedNamedField.Count() > 0)
                    await Logger.AssertNotReached("Looks like we may have missed a named field");
            }

            foreach (var node in titleRow.ChildNodes)
            {
                var selectNode = (from sel in node.Descendants("select") select sel).FirstOrDefault();
                if (selectNode != null)
                {
                    SfDomainUpDown dud = new SfDomainUpDown();
                    dud.Margin = new Thickness(10, 5, 10, 5);
                    dud.ContentTemplate = this.Resources["DomainUpDownTemplate"] as DataTemplate;

                    List<FormFieldItem> items = new List<FormFieldItem>();
                    foreach (var value in (from val in selectNode.Descendants("option") select val))
                    {
                        items.Add(new FormFieldItem(selectNode.Attributes["name"].Value, value.Attributes["value"].Value, value.InnerText));
                    }

                    dud.ItemsSource = items;
                    dud.Value = items.First();

                    var span = node.Descendants("span").FirstOrDefault();

                    if (span != null)
                    {
                        PostingFields.Children.Add(this.GetPostingFieldLabel(span.InnerText.Trim()));
                    }
                    else if (node.InnerText.Contains("Category"))
                    {
                        PostingFields.Children.Add(this.GetPostingFieldLabel("Category"));
                    }

                    PostingFields.Children.Add(dud);

                    continue;
                }
            }

            if (PostingTitleText.Tag == null || SpecificLocText.Tag == null)
            {
                throw new PostParseException("Could not find Posting Title and Description fields");
            }

            var description = (from textarea in formNode.Descendants("textarea") select textarea).FirstOrDefault();
            if (description == null)
            {
                throw new PostParseException("Could not find description field");
            }
            DescriptionText.Text = Utilities.HtmlToText(description.InnerText);
            DescriptionText.Tag = description.Attributes["name"].Value;
        }

        private async Task ParseEditEmailRow(HtmlNode formNode)
        {
            var emailNode = (from email in formNode.Descendants().Where(x => x.Attributes["name"] != null && x.Attributes["name"].Value == "FromEMail") select email).First();

            this.PostingFields.Children.Add(this.GetPostingFieldLabel("Reply-To Email"));

            if (this._context.Account == null)
            {
                await Logger.AssertValue(emailNode.Attributes["type"].Value, "text", "Expected hidden email node");
                WatermarkTextBox tb = new WatermarkTextBox();
                tb.WatermarkText = "Your Email";
                tb.Name = "FromEMail";
                tb.Tag = "FromEMail";
                tb.Margin = new Thickness(10, 5, 10, 5);
                tb.BorderBrush = new SolidColorBrush(Colors.DarkGreen);
                tb.Text = emailNode.Attributes["value"].Value.Contains("Your email address") ? string.Empty : emailNode.Attributes["value"].Value;
                this.PostingFields.Children.Add(tb);
            }
            else
            {
                TextBlock tb = this.GetPostingFieldLabel(this._context.Account.UserName);
                tb.Tag = "FromEMail";
                tb.Name = "FromEMail";
                tb.Margin = new Thickness(10, 5, 10, 5);
                tb.FontWeight = Windows.UI.Text.FontWeights.Normal;
                this.PostingFields.Children.Add(tb);
            }

            var inputs = (from div in formNode.Descendants("div").Where(x => x.Id == "oiab")
                          from radio in div.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "radio")
                          select radio);
            var values = new List<FormFieldItem>();

            SfDomainUpDown dud = new SfDomainUpDown();
            dud.ItemsSource = values;
            dud.Margin = new Thickness(10, 0, 10, 10);
            dud.ContentTemplate = this.Resources["DomainUpDownTemplate"] as DataTemplate;

            foreach (var value in inputs)
            {
                if (value.Attributes["type"] != null && value.Attributes["type"].Value == "radio")
                {
                    string emailType = value.Attributes["value"].Value;
                    if (emailType == "P")
                        values.Add(new FormFieldItem(value.Attributes["name"].Value, value.Attributes["value"].Value, "show email"));
                    else if (emailType == "A")
                        values.Add(new FormFieldItem(value.Attributes["name"].Value, value.Attributes["value"].Value, "hide email"));
                    else if (emailType == "C")
                        values.Add(new FormFieldItem(value.Attributes["name"].Value, value.Attributes["value"].Value, "anonymize email"));
                    else
                        await Logger.AssertNotReached("Unexpected email option");

                    if (value.Attributes["checked"] != null)
                        dud.Value = values.Last();
                }
            }

            if (values.Any())
                this.PostingFields.Children.Add(dud);
        }

        private async Task ParseEditEventDates(HtmlNode formNode)
        {
            var ed = (from div in formNode.Descendants("div").Where(x => x.Id == "ed") select div).FirstOrDefault();

            if (ed != null)
            {
                EventDateFields startDate = new EventDateFields();
                EventDateFields endDate = new EventDateFields();

                var inputs = (from input in ed.Descendants("input") select input);
                foreach (var input in inputs)
                {
                    var date = input.Attributes["class"] != null && input.Attributes["class"].Value == "req" ? startDate : endDate;
                    var type = input.ParentNode.InnerText;

                    if (type.Contains("Year"))
                    {
                        date.Year = input.Attributes["name"].Value;
                    }
                    else if (type.Contains("Mo"))
                    {
                        date.Month = input.Attributes["name"].Value;
                    }
                    else if (type.Contains("Day"))
                    {
                        date.Day = input.Attributes["name"].Value;
                    }
                    else
                    {
                        await Logger.AssertNotReached("Unknown date input field");
                    }
                }

                this.PostingFields.Children.Add(this.GetPostingFieldLabel("Event Start Date"));
                SfDatePicker sdp = new SfDatePicker();
                sdp.Name = "StartDate";
                sdp.Margin = new Thickness(10, 0, 10, 5);
                sdp.Tag = startDate;
                sdp.ValueChanged += (s, e) =>
                        {
                            SfDatePicker dp = s as SfDatePicker;
                            DateTime dt = (DateTime)dp.Value;

                            if (DateTime.Now.Date > dt)
                            {
                                dp.Value = DateTime.Now.Date;
                            }

                            SfDatePicker _edp = this.PostingFields.FindName("EndDate") as SfDatePicker;
                            if ((DateTime)_edp.Value < (DateTime)dp.Value)
                                _edp.Value = dp.Value;
                        };

                this.PostingFields.Children.Add(sdp);

                this.PostingFields.Children.Add(this.GetPostingFieldLabel("Event End Date"));
                SfDatePicker edp = new SfDatePicker();
                edp.Name = "EndDate";
                edp.Margin = new Thickness(10, 0, 10, 5);
                edp.Tag = endDate;
                edp.ValueChanged += (s, e) =>
                    {
                        SfDatePicker dp = s as SfDatePicker;
                        SfDatePicker _sdp = this.PostingFields.FindName("StartDate") as SfDatePicker;

                        if ((DateTime)dp.Value < (DateTime)_sdp.Value)
                        {
                            dp.Value = _sdp.Value;
                        }
                    };

                this.PostingFields.Children.Add(edp);
            }
        }

        private async Task ParseCompensation(HtmlNode formNode)
        {
            var cd = (from compdet in formNode.Descendants("span").Where(x => x.Id == "compdet")
                      select compdet).FirstOrDefault();

            if (cd != null)
            {
                var p = cd.ParentNode;

                await Logger.Assert(p.Name == "p", "Parent should be a p node");

                this.PostingFields.Children.Add(this.GetPostingFieldLabel("Compensation"));

                var radios = (from r in p.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "radio")
                              select r);

                if (radios != null && radios.Count() > 0)
                {
                    var values = new List<FormFieldItem>();

                    SfDomainUpDown dud = new SfDomainUpDown();
                    dud.ItemsSource = values;
                    dud.Margin = new Thickness(10, 0, 10, 10);
                    dud.ContentTemplate = this.Resources["DomainUpDownTemplate"] as DataTemplate;

                    foreach (var radio in radios)
                    {
                        values.Add(new FormFieldItem(radio.Attributes["name"].Value, radio.Attributes["value"].Value, radio.ParentNode.InnerText.Trim().Trim(':')));

                        if (radio.Attributes["checked"] != null)
                        {
                            dud.Value = values.Last();
                        }
                    }

                    if (dud.Value == null && values.Count > 0)
                    {
                        dud.Value = values.Last();
                    }

                    this.PostingFields.Children.Add(dud);
                }

                var text = (from t in p.Descendants("input").Where(x => x.Attributes["type"] == null) select t).FirstOrDefault();

                if (text != null)
                {
                    WatermarkTextBox tb = new WatermarkTextBox();
                    tb.WatermarkText = "Compensation Details";
                    tb.Tag = text.Attributes["name"].Value;
                    tb.Margin = new Thickness(10, 0, 10, 10);
                    tb.Text = text.Attributes["value"].Value;
                    this.PostingFields.Children.Add(tb);
                }
            }
        }

        private void ParseEditPostMap(HtmlNode formNode)
        {
            var map = (from div in formNode.Descendants("div").Where(x => x.Id == "map") select div).FirstOrDefault();

            if (map != null && (from input in map.Descendants("input") select input).Any())
            {
                PostMapControl mapControl = new PostMapControl();
                this.PostingFields.Children.Add(mapControl);
            }
        }

        private void ParseEditPerms(HtmlNode formNode)
        {
            var checkboxes = (from perms in formNode.Descendants("div").Where(x => x.Id == "perms")
                              from cb in perms.Descendants("input").Where(x => x.Attributes["type"] != null && x.Attributes["type"].Value == "checkbox")
                              select cb);

            foreach (var cb in checkboxes)
            {
                // This is handled by the map control.
                if (cb.Id == "wantamap")
                    continue;

                ToggleSwitch ts = new ToggleSwitch();
                ts.Header = cb.ParentNode.InnerText.Trim();
                ts.HeaderTemplate = this.Resources["ToggleSwitchHeaderTemplate"] as DataTemplate;
                ts.Tag = cb.Attributes["name"].Value;
                ts.Margin = new Thickness(10, 5, 10, 0);
                ts.IsOn = cb.Attributes["checked"] != null;
                this.PostingFields.Children.Add(ts);
            }
        }

        private TextBlock GetPostingFieldLabel(string label)
        {
            TextBlock tb = new TextBlock();
            tb.Margin = new Thickness(10, 10, 10, 5);
            tb.Text = label;
            tb.FontFamily = new FontFamily("Segoe UI Light");
            tb.FontWeight = Windows.UI.Text.FontWeights.Light;
            tb.FontSize = 20;
            return tb;
        }

        private void SetTitle(HtmlDocument doc)
        {
            try
            {
                var contents = (from c in doc.DocumentNode.Descendants("section").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "contents") select c).FirstOrDefault();

                if (contents != null)
                {
                    var aside = (from a in contents.Descendants("aside") select a).FirstOrDefault();

                    if (aside != null)
                        aside.Remove();

                    this.PanelTitle.Text = Utilities.HtmlToText(contents.InnerText).Replace("\n", string.Empty).Trim();
                }
                else
                {
                    this.PanelTitle.Text = "Create Post";
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async Task FinalizeSetPostState(CreatePostState step)
        {
            this._state = step;

            PickOneState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            JobsCategoryState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            MixState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            EditState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            EditImageState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            PreviewState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            ApprovalState.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            switch (this._state)
            {
                case CreatePostState.PickOne:
                    PickOneState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.JobsCategory:
                    JobsCategoryState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.Mix:
                    MixState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.Edit:
                    EditState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.EditImages:
                    EditImageState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.Preview:
                    PreviewState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                case CreatePostState.Approval:
                    // Increment the number of in app posts we have made.
                    Settings.Instance.InAppPostCount++;

                    ContinueButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    BackButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    ApprovalState.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    break;
                default:
                    await Logger.AssertNotReached("Unknown step");
                    break;
            }

            this.LoadingProgress.Visibility = Visibility.Visible;
        }
        #endregion

        #region Navigation
        private async void ContinueButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            switch (this._state)
            {
                case CreatePostState.PickOne:
                    await this.ContinuePickOne(this.PickOneGrid.SelectedItem as PickItemVM);
                    break;
                case CreatePostState.JobsCategory:
                    await this.ContinueJobCategory();
                    break;
                case CreatePostState.Mix:
                    await this.ContinueMix();
                    break;
                case CreatePostState.Edit:
                    await this.ContinueEditPost();
                    break;
                case CreatePostState.Preview:
                    await this.ContinuePreview();
                    break;
                case CreatePostState.EditImages:
                    await this.ContinueEditImages();
                    break;
                default:
                    await new MessageDialog("NYI").ShowAsync();
                    break;
            }
        }

        private async void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (this._backHistory.Count == 0)
            {
                await Logger.AssertNotReached("Should have more than one in our history. The last one represents current page.");
            }
            else
            {
                this.ToggleNavigationButtons(false);

                // The way this works is a little weird. If you pop the queue, the URL returned is actually the current URL. The second
                // one would actual reference the page you came from. Therefore, if we want to go back, we need to pop twice.

                this.LoadingProgress.Visibility = Visibility.Visible;
                HttpResponseMessage response = null;

                try
                {
                    Uri url = this._backHistory.Peek();
                    response = await this._context.Client.GetAsync(url);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    await new MessageDialog("We received a bad response from Craigslist.", "Craigslist 8X").ShowAsync();
                    this.LoadingProgress.Visibility = Visibility.Collapsed;
                    return;
                }
                else
                {
                    this._backHistory.Pop();
                    await this.ParseResponse(response, true);
                }
            }
        }

        private async void PickOneItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            await this.ContinuePickOne((sender as FrameworkElement).DataContext as PickItemVM);
        }

        private async Task ContinuePickOne(PickItemVM vm)
        {
            if (vm != null)
            {
                this.ToggleNavigationButtons(false);

                HttpContent content = new StringContent(string.Format("{0}={1}{2}",
                    Uri.EscapeDataString(vm.Name),
                    Uri.EscapeDataString(vm.Value),
                    this.GetHiddenFields()
                    ));
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                await this.IssueRequest(content);
            }
            else
            {
                await new MessageDialog("Select an item from the list to continue.", "Craigslist 8X").ShowAsync();
            }
        }

        private async Task ContinueMix()
        {
            if (LeftMixGrid.SelectedIndex >= 0 && RightMixGrid.SelectedIndex >= 0
                || LeftMixGrid.SelectedIndex == -1 && RightMixGrid.SelectedIndex == -1)
            {
                this.ToggleNavigationButtons(false);

                HttpContent content = null;

                if (LeftMixGrid.SelectedIndex >= 0)
                {
                    content = new StringContent(string.Format("{0}={1}&{2}={3}{4}",
                        Uri.EscapeDataString(((PickItemVM)LeftMixGrid.SelectedItem).Name),
                        Uri.EscapeDataString(((PickItemVM)LeftMixGrid.SelectedItem).Value),
                        Uri.EscapeDataString(((PickItemVM)RightMixGrid.SelectedItem).Name),
                        Uri.EscapeDataString(((PickItemVM)RightMixGrid.SelectedItem).Value),
                        this.GetHiddenFields()
                        ));
                }
                else
                {
                    content = new StringContent(this.GetHiddenFields().Substring(1));
                }

                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

                await this.IssueRequest(content);
            }
            else
            {
                await new MessageDialog("Select an item from each list or leave both lists blank to continue.", "Craigslist 8X").ShowAsync();
            }
        }

        private async Task ContinueJobCategory()
        {
            if (JobsCategoryGrid.SelectedItems.Count > 0)
            {
                this.ToggleNavigationButtons(false);

                StringBuilder postData = new StringBuilder();

                foreach (var item in JobsCategoryGrid.SelectedItems)
                {
                    PickItemVM vm = item as PickItemVM;

                    postData.Append(string.Format("{0}={1}&", Uri.EscapeDataString(vm.Name), Uri.EscapeDataString(vm.Value)));
                }

                postData.Append(this.GetHiddenFields().Substring(1));

                HttpContent content = new StringContent(postData.ToString());
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

                await this.IssueRequest(content);
            }
            else
            {
                await new MessageDialog("Select one or more categories from the list to continue.", "Craigslist 8X").ShowAsync();
            }
        }

        private async Task ContinuePreview()
        {
            this.ToggleNavigationButtons(false);

            HttpContent content = new StringContent(this.GetHiddenFields().Substring(1));
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

            await this.IssueRequest(content);
        }

        private async Task ContinueEditImages()
        {
            this.ToggleNavigationButtons(false);

            string hidden = this.GetHiddenFields();
            if (!string.IsNullOrEmpty(hidden))
            {
                hidden = hidden.TrimStart('&');
            }

            HttpContent content = new StringContent(hidden);
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

            await this.IssueRequest(content);
        }

        private async Task ContinueEditPost()
        {
            var email = this.PostingFields.FindName("FromEMail") as WatermarkTextBox;

            if (string.IsNullOrWhiteSpace(this.PostingTitleText.Text)
                || string.IsNullOrWhiteSpace(this.DescriptionText.Text)
                || (email != null && !email.Text.Contains("@")))
            {
                await new MessageDialog("Please fill out all required field before continuing.", "Craigslist 8X").ShowAsync();
                return;
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(string.Format("{0}={1}&{2}={3}",
                    Uri.EscapeDataString(PostingTitleText.Tag.ToString()),
                    Uri.EscapeDataString(PostingTitleText.Text),
                    Uri.EscapeDataString(DescriptionText.Tag.ToString()),
                    Uri.EscapeDataString(DescriptionText.Text)
                    ));

                foreach (var child in this.PostingFields.Children)
                {
                    if (child is FrameworkElement)
                    {
                        FrameworkElement el = child as FrameworkElement;

                        if (el.Tag != null)
                        {
                            if (el.Tag is EventDateFields)
                            {
                                EventDateFields edf = el.Tag as EventDateFields;
                                SfDatePicker dp = el as SfDatePicker;

                                sb.Append(string.Format("&{0}={1}&{2}={3}&{4}={5}",
                                    Uri.EscapeDataString(edf.Year),
                                    Uri.EscapeDataString(((DateTime)dp.Value).Year.ToString()),
                                    Uri.EscapeDataString(edf.Month),
                                    Uri.EscapeDataString(((DateTime)dp.Value).Month.ToString()),
                                    Uri.EscapeDataString(edf.Day),
                                    Uri.EscapeDataString(((DateTime)dp.Value).Day.ToString())
                                    ));
                            }
                            else if (el is TextBox)
                            {
                                sb.Append(string.Format("&{0}={1}",
                                    Uri.EscapeDataString(el.Tag.ToString()),
                                    Uri.EscapeDataString((el as TextBox).Text.Trim())
                                    ));

                                if (el.Name == "FromEMail")
                                {
                                    sb.Append(string.Format("&{0}={1}",
                                    Uri.EscapeDataString("ConfirmEMail"),
                                    Uri.EscapeDataString((el as TextBox).Text.Trim())
                                    ));
                                }
                            }
                            else if (el is TextBlock)
                            {
                                sb.Append(string.Format("&{0}={1}",
                                    Uri.EscapeDataString(el.Tag.ToString()),
                                    Uri.EscapeDataString((el as TextBlock).Text)
                                    ));

                                if (el.Name == "FromEMail")
                                {
                                    sb.Append(string.Format("&{0}={1}",
                                    Uri.EscapeDataString("ConfirmEMail"),
                                    Uri.EscapeDataString((el as TextBlock).Text.Trim())
                                    ));
                                }
                            }
                            else if (el is ToggleSwitch)
                            {
                                sb.Append(string.Format("&{0}={1}",
                                    Uri.EscapeDataString(el.Tag.ToString()),
                                    Uri.EscapeDataString((el as ToggleSwitch).IsOn ? "on" : string.Empty)
                                    ));
                            }
                        }
                        else if (el is PostMapControl)
                        {
                            sb.Append(((PostMapControl)el).GetPostData());
                        }
                        else if (el is SfDomainUpDown)
                        {
                            sb.Append(string.Format("&{0}={1}",
                                Uri.EscapeDataString(((el as SfDomainUpDown).Value as FormFieldItem).Name),
                                Uri.EscapeDataString(((el as SfDomainUpDown).Value as FormFieldItem).Value)
                                ));
                        }
                    }
                }

                sb.Append(this.GetHiddenFields());

                HttpContent content = new StringContent(sb.ToString());
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

                await this.IssueRequest(content);
            }
        }

        private async Task IssueRequest(HttpContent content, bool addHistory = true)
        {
            this.LoadingProgress.Visibility = Visibility.Visible;
            this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

            HttpResponseMessage response = null;

            try
            {
                response = await this._context.Client.PostAsync(this._postLink, content);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                await new MessageDialog("We received a bad response from Craigslist.", "Craigslist 8X").ShowAsync();
                this.LoadingProgress.Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                if (addHistory)
                    this._backHistory.Push(new Uri(this._currentPage.ToString()));

                await this.ParseResponse(response, false);
            }
        }

        private void ToggleNavigationButtons(bool enable)
        {
            this.ContinueButton.IsEnabled = enable;
            this.BackButton.IsEnabled = enable && this._backHistory.Count > 0;
        }
        #endregion

        #region Methods
        private async void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            FrameworkElement el = sender as FrameworkElement;

            if (el.DataContext is AddImageBoxVM)
            {
                AddImageBoxVM vm = el.DataContext as AddImageBoxVM;

                PopupMenu menu = new PopupMenu();
                menu.Commands.Add(new UICommand() { Label = "Camera", Id = 0 });
                menu.Commands.Add(new UICommand() { Label = "Pick File", Id = 1 });
                var result = await menu.ShowForSelectionAsync(WB.Craigslist8X.Common.Utilities.GetElementRect(el), Placement.Below);

                if (result == null)
                    return;
                else if ((int)result.Id == 0)
                {
                    var dialog = new CameraCaptureUI();
                    dialog.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Png;
                    var file = await dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);
                    if (file != null)
                    {
                        MultipartFormDataContent content = new MultipartFormDataContent(Guid.NewGuid().ToString());
                        foreach (var hidden in vm.HiddenFields)
                        {
                            content.Add(new StringContent(hidden.Value), hidden.Name);
                        }
                        content.Add(new StreamContent(await file.OpenStreamForReadAsync()), "file", string.Format("{0}.png", Guid.NewGuid()));
                        await this.IssueRequest(content, addHistory: false);
                    }
                }
                else if ((int)result.Id == 1)
                {
                    var picker = new FileOpenPicker();

                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.ViewMode = PickerViewMode.Thumbnail;
                    picker.FileTypeFilter.Add(".jpg");
                    picker.FileTypeFilter.Add(".jpeg");
                    picker.FileTypeFilter.Add(".png");

                    var file = await picker.PickSingleFileAsync();

                    if (file != null)
                    {
                        MultipartFormDataContent content = new MultipartFormDataContent(Guid.NewGuid().ToString());
                        foreach (var hidden in vm.HiddenFields)
                        {
                            content.Add(new StringContent(hidden.Value), hidden.Name);
                        }
                        content.Add(new StreamContent(await file.OpenStreamForReadAsync()), "file", file.Name);
                        await this.IssueRequest(content, addHistory: false);
                    }
                }
            }
            else if (el.DataContext is ImageBoxVM)
            {
                // Do nothing?
            }
            else
            {
                await Logger.AssertNotReached("Unknown image type");
            }
        }

        private async void DeleteImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            FrameworkElement el = sender as FrameworkElement;

            if (el.DataContext is ImageBoxVM)
            {
                ImageBoxVM vm = el.DataContext as ImageBoxVM;

                string data = string.Empty;
                foreach (var h in vm.HiddenFields)
                {
                    data += string.Format("&{0}={1}", Uri.EscapeDataString(h.Name), Uri.EscapeDataString(h.Value));
                }

                HttpContent content = new StringContent(data.Substring(1));
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                this._context.Client.DefaultRequestHeaders.ExpectContinue = false;

                await this.IssueRequest(content, addHistory: false);
            }
            else
            {
                await Logger.AssertNotReached("Unknown image type");
            }
        }

        private void JobsCategoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            double total = 0;

            foreach (var item in JobsCategoryGrid.SelectedItems)
            {
                PickItemVM vm = item as PickItemVM;
                HtmlNode node = vm.Node;

                double cost = 0;
                if (double.TryParse(node.Attributes["class"].Value, out cost))
                {
                    total += cost;
                }
            }

            TotalCost.Text = string.Format("{0:C}", total);
        }

        private async void OpenInBrowserButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (this._currentPage == null)
                return;

            await Launcher.LaunchUriAsync(this._currentPage);
        }

        private string GetHiddenFields()
        {
            string hiddens = string.Empty;
            if (this._hiddenFields != null)
            {
                foreach (var h in this._hiddenFields)
                {
                    hiddens += string.Format("&{0}={1}", Uri.EscapeDataString(h.Name), Uri.EscapeDataString(h.Value));
                }
            }

            return hiddens;
        }
        #endregion

        #region Fields
        CreatePostContext _context;
        CreatePostState _state;
        UIElement _parent;
        List<FormHiddenField> _hiddenFields;
        Stack<Uri> _backHistory;
        Uri _postLink;
        Uri _currentPage;
        #endregion
    }

    enum CreatePostState
    {
        PickOne,
        JobsCategory,
        Mix,
        Edit,
        EditImages,
        Preview,
        Approval,
    }

    public class CreatePostContext
    {
        public PasswordCredential Account
        {
            get;
            set;
        }

        public CraigCity City
        {
            get;
            set;
        }

        public Uri Url
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
    }

    public class PostParseException : Exception
    {
        public PostParseException(string message)
            : base(message)
        {
        }
    }
}
