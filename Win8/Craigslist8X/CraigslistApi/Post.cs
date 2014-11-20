using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Html;
using Windows.Data.Xml.Dom;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

using HtmlAgilityPack;

using WB.SDK;
using WB.SDK.Logging;

namespace WB.CraigslistApi
{
    public class Post
    {
        public Post()
        {
            this._detailStatusLock = new object();

            this.PostText = string.Empty;
            this.Images = new List<Uri>();
            this.Pictures = new List<Uri>();
            this.DetailStatus = PostDetailStatus.NotLoaded;
        }

        #region Serialization
        public static string Serialize(Post p)
        {
            return string.Format("<p><url>{0}</url><title>{1}</title><date>{2}</date><tn>{3}</tn><loc>{4}</loc><price>{5}</price><pic>{6}</pic><img>{7}</img></p>",
                Uri.EscapeDataString(p.Url.ToString()),
                Uri.EscapeDataString(p.Title ?? string.Empty),
                Uri.EscapeDataString(p.ShortDate ?? string.Empty),
                Uri.EscapeDataString(p.ThumbnailUri == null ? string.Empty : p.ThumbnailUri.ToString()),
                Uri.EscapeDataString(p.Location ?? string.Empty),
                p.Price,
                p.HasPictures,
                p.HasImages);
        }

        public static Post Deserialize(string q)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(q);

            Post post = new Post();

            post.Url = new Uri(Uri.UnescapeDataString(doc.SelectSingleNode("p/url").InnerText));
            post.Title = Uri.UnescapeDataString(doc.SelectSingleNode("p/title").InnerText);
            post.ShortDate = Uri.UnescapeDataString(doc.SelectSingleNode("p/date").InnerText);
            post.Location = Uri.UnescapeDataString(doc.SelectSingleNode("p/loc").InnerText);
            post.Price = int.Parse(doc.SelectSingleNode("p/price").InnerText);
            post.HasPictures = bool.Parse(doc.SelectSingleNode("p/pic").InnerText);
            post.HasImages = bool.Parse(doc.SelectSingleNode("p/img").InnerText);

            if (!string.IsNullOrEmpty(doc.SelectSingleNode("p/tn").InnerText))
                post.ThumbnailUri = new Uri(Uri.UnescapeDataString(doc.SelectSingleNode("p/tn").InnerText));

            return post;
        }
        #endregion

        #region Parsing
        public event EventHandler DetailsLoaded;

        internal void Parse(QueryResult qr, HtmlNode row, string shortDate)
        {
            this.QueryResult = qr;

            // Parse map coordinates
            if (row.Attributes["data-latitude"] != null && row.Attributes["data-longitude"] != null)
            {
                Coordinate coordinates;
                double value;

                if (double.TryParse(row.Attributes["data-latitude"].Value, out value))
                {
                    coordinates.Latitude = value;
                }
                if (double.TryParse(row.Attributes["data-longitude"].Value, out value))
                {
                    coordinates.Longitude = value;
                }
            }

            // Try to find the right link that contains the title
            var links = (from pl in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("pl"))
                         from anchor in pl.Descendants("a").Where(x => x.Attributes["href"] != null) 
                         select anchor);
            if (links == null || links.Count() < 1)
                links = (from anchor in row.Descendants("a").Where(x => x.Attributes["href"] != null) select anchor);
            foreach (var link in links)
            {
                Regex postLink = new Regex(@"\d+\.html?$");
                if (postLink.IsMatch(link.Attributes["href"].Value))
                {
                    this.Title = Utilities.HtmlToText(link.InnerText);

                    Uri url = null;

                    if (Uri.TryCreate(link.Attributes["href"].Value, UriKind.Absolute, out url))
                    {
                        this.Url = url;
                    }
                    else if (Uri.TryCreate(
                        string.Format("{0}://{1}{2}", 
                        qr.HttpResponse.RequestMessage.RequestUri.Scheme, 
                        qr.HttpResponse.RequestMessage.RequestUri.Host, 
                        link.Attributes["href"].Value), UriKind.Absolute, 
                        out url))
                    {
                        this.Url = url;
                    }

                    break;
                }
            }

            var itemi = (from a in row.Descendants("a").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("i") && x.Attributes["data-id"] != null) select a).FirstOrDefault();
            if (itemi != null)
            {
                var dataId = itemi.Attributes["data-id"].Value;
                if (dataId.StartsWith("0:", StringComparison.OrdinalIgnoreCase))
                    ThumbnailUri = new Uri(string.Format("http://images.craigslist.org/{0}_50x50c.jpg", dataId.Substring(2)));
                else
                    ThumbnailUri = new Uri(string.Format("http://images.craigslist.org/medium/{0}", itemi.Attributes["data-id"].Value));
            }

            var itemdate = (from span in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("date")) select span).FirstOrDefault();
            if (itemdate != null)
            {
                this.ShortDate = shortDate;
                if (string.IsNullOrEmpty(this.ShortDate) && itemdate != null)
                    this.ShortDate = itemdate.InnerText.TrimStart(' ');
            }

            var itemcg = (from a in row.Descendants("a").Where(x => x.Attributes["class"] != null && x.Attributes["data-cat"] != null && x.Attributes["class"].Value.Contains("gc")) select a).FirstOrDefault();
            if (itemcg != null)
            {
                this.CategoryCode = itemcg.Attributes["data-cat"].Value;
            }

            var itempx = (from span in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("px")) select span).FirstOrDefault();
            if (itempx != null)
            {
                this.HasImages = itempx.InnerText.Contains("img");
                this.HasPictures = new Regex("(pic)|(Photo)").IsMatch(itempx.InnerText);
                this.HasMap = itempx.InnerText.Contains("map");
#if DEBUG
                if (!string.IsNullOrWhiteSpace(itempx.InnerText) && !this.HasImages && !this.HasPictures)
                {
                    Logger.LogMessage("Post", "We found a non empty itempx span, but we did not set HasImages or HasPictures to true. Different language?");
                }
#endif
            }

            var itempn = (from span in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("pnr"))
                          from small in span.Descendants("small")
                          select small).FirstOrDefault();
            if (itempn != null)
            {
                var location = itempn.InnerText;
                if (location.StartsWith(" (") && location.EndsWith(")"))
                    this.Location = location.Substring(2, location.Length - 3);
                else
                    this.Location = location;
            }

            var itempp = (from span in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("price")) select span).FirstOrDefault();
            if (itempp != null)
            {
                Regex price = new Regex(@"^\$|(&#x0024;)(\d+)$");
                Match m = price.Match(itempp.InnerText);
                if (m.Success)
                    int.TryParse(m.Groups[2].Value, out this._price);
            }

            var itemph = (from span in row.Descendants("span").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("ph")) select span).FirstOrDefault();
            if (itemph != null)
            {
                Regex price = new Regex(@"\$(\d*)");
                Match m = price.Match(itemph.InnerText);
                if (m.Success)
                    int.TryParse(m.Groups[1].Value, out this._price);

                // TODO: Parse other housing specific info (bedrooms, etc)
            }
        }

        public async Task LoadDetailsAsync()
        {
            // Prevent someone from trying to load details multiple times for the same post
            if (this.DetailStatus == PostDetailStatus.Loaded || this.DetailStatus == PostDetailStatus.Loading)
            {
                await Logger.AssertNotReached("Trying to reload Post details.");
                return;
            }

            this.DetailStatus = PostDetailStatus.Loading;

            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(this.Url))
                {
                    if (response != null && response.IsSuccessStatusCode)
                    {
                        await this.ParseDetails(await response.Content.ReadAsStringAsync());
                        this.DetailStatus = PostDetailStatus.Loaded;
                    }
                    else
                    {
                        if (response == null)
                            Logger.LogMessage("CraigslistApi", "Post.LoadDetailsAsync failed to get response from '{0}'", this.Url);
                        else
                            Logger.LogMessage("CraigslistApi", "Received non-succcess status code: {0}", response.StatusCode);

                        this.DetailStatus = PostDetailStatus.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                this.DetailStatus = PostDetailStatus.Failed;
            }

            if (this.DetailsLoaded != null)
                this.DetailsLoaded(this, null);
        }

        private async Task ParseDetails(string content)
        {
            await Task.Factory.StartNew(async () =>
            {
                using (new MonitoredScope("Parsing Post detail: {0}", this.Url))
                {
                    HtmlDocument html = new HtmlDocument();
                    html.LoadHtml(content);

                    var userBody =
                        (from userbody in html.DocumentNode.Descendants().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "userbody" || x.Id != null && x.Id == "userbody")
                         select userbody).FirstOrDefault();

                    if (userBody != null)
                    {
                        var removed = (from div in html.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "removed")
                                       select div).FirstOrDefault();
                        if (removed != null)
                        {
                            this.UserHtml = removed.InnerHtml;
                            this.PostText = HtmlUtilities.ConvertToText(this.UserHtml);
                            return;
                        }

                        var postingBody = (from body in html.DocumentNode.Descendants("section").Where(x => x.Id == "postingbody") select body).FirstOrDefault();

                        if (postingBody == null)
                        {
                            await Logger.AssertNotReached("No body?");
                            return;
                        }

                        if (this.HasMap)
                        {
                            var leaflet = (from div in html.DocumentNode.Descendants("div").Where(x => x.Id == "leaflet") select div).FirstOrDefault();
                            if (leaflet != null && leaflet.Attributes.Contains("data-latitude") && leaflet.Attributes.Contains("data-longitude"))
                            {
                                Coordinate coords = new Coordinate();
                                coords.Latitude = double.Parse(leaflet.Attributes["data-latitude"].Value);
                                coords.Longitude = double.Parse(leaflet.Attributes["data-longitude"].Value);
                                this.MapCoordinate = coords;
                            }
                        }

                        this.UserHtml = postingBody.InnerHtml;

                        var blurbs = (from tags in html.DocumentNode.Descendants("ul").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "blurbs")
                                      select tags).FirstOrDefault();
                        if (blurbs != null)
                        {
                            this.UserHtml += "<br/>" + blurbs.InnerHtml;
                        }

                        using (HttpClient client = new HttpClient())
                        using (HttpResponseMessage response = await client.GetAsync(string.Format("{0}://{1}/reply/{2}", this.Url.Scheme, this.Url.Host, this.ID)))
                        {
                            if (response != null && response.IsSuccessStatusCode)
                            {
                                var replyText = await response.Content.ReadAsStringAsync();
                                HtmlDocument replyDom = new HtmlDocument();
                                replyDom.LoadHtml(replyText);

                                Regex phoneRegex = new Regex(@"\(?(\d{3})\)?\s?[\-\.]?\s?(\d{3})\s?[\-\.]?\s?(\d{4})");
                                Match match = phoneRegex.Match(replyText);
                                if (match.Success)
                                {
                                    this.Phone = string.Format("{0}-{1}-{2}", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
                                }

                                var email = (from mailto in replyDom.DocumentNode.Descendants("a").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("mailto"))
                                             select mailto).FirstOrDefault();
                                if (email != null)
                                {
                                    this.Email = email.InnerText.Trim();
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(this.Phone))
                        {
                            Regex phoneRegex = new Regex(@"\(?(\d{3})\)?\s?[\-\.]?\s?(\d{3})\s?[\-\.]?\s?(\d{4})");
                            Match match = phoneRegex.Match(this.UserHtml);
                            if (match.Success)
                            {
                                this.Phone = string.Format("{0}-{1}-{2}", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
                            }
                        }

                        var dateNode = (from div in html.DocumentNode.Descendants("p").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "postinginfo")
                                        from d in div.Descendants("date")
                                        select d).FirstOrDefault();
                        if (dateNode != null)
                        {
                            string date = dateNode.InnerText;
                            date = date.Contains("AM") ? date.Substring(0, date.IndexOf("AM") + 2) : date;
                            date = date.Contains("PM") ? date.Substring(0, date.IndexOf("PM") + 2) : date;
                            this.Timestamp = DateTime.Parse(date);
                        }

                        var postingInfos = (from div in html.DocumentNode.Descendants("div").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "postinginfos") select div).FirstOrDefault();
                        if (postingInfos != null)
                        {
                            postingInfos.Remove();
                        }

                        // Get the short body description. Basically a text dump with no images or layout.
                        Regex descriptionRegex = new Regex(@"(\s){3,}");
                        this.PostText = HtmlUtilities.ConvertToText(this.UserHtml);
                        this.PostText = descriptionRegex.Replace(this.PostText, "\r\n\r\n").Trim();

                        this.ParseImages(userBody);
                    }
                    else
                    {
                        // node = html.FindNode(new Regex(@"(This posting has been deleted by its author\.)|(This posting has been flagged for removal\.)"));
                        this.UserHtml = string.Empty;
                        this.IsDeleted = true;
                    }
                }
            },
            CancellationToken.None
            );
        }

        private void ParseImages(HtmlNode userBody)
        {
            // Get all the images in the post
            if (this.HasImages)
            {
                var imgs = (from img in userBody.Descendants("img").Where(x => x.Attributes["src"] != null) select img);
                this.Images.Clear();
                foreach (var img in imgs)
                {
                    this.Images.Add(new Uri(img.Attributes["src"].Value));
                }
            }

            // Get the pictures. Pics are those images at the bottom of a post that are part of a lightbox type control.
            if (this.HasPictures)
            {
                var thumbs = (from th in userBody.Descendants("div").Where(x => x.Id == "thumbs") select th).FirstOrDefault();

                // On 1/17/2013 Craigslist pushed out an update that messes up image parsing. We now have two potential ways of getting images.
                if (thumbs != null)
                {
                    var pics = (from tn in thumbs.Descendants("a") select tn);
                    this.Pictures.Clear();
                    foreach (var pic in pics)
                    {
                        this.Pictures.Add(new Uri(pic.Attributes["href"].Value));
                    }
                }
                else
                {
                    var pics = (from ci in userBody.Descendants("div").Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == "ci")
                                from img in ci.Descendants("img").Where(x => x.Attributes["src"] != null)
                                select img);
                    this.Pictures.Clear();
                    foreach (var pic in pics)
                    {
                        this.Pictures.Add(new Uri(pic.Attributes["src"].Value));
                    }
                }
            }

            // There is no thumbnail, so we try just grab any image we can from the post and hopefully
            // it is useful
            if (this.ThumbnailUri == null)
            {
                if (this.HasPictures && this.Pictures.Any())
                    this.ThumbnailUri = this.Pictures.First();
                else if (this.HasImages && this.Images.Any())
                    this.ThumbnailUri = this.Images.First();
            }
        }
        #endregion

        #region Listing Properties
        public bool IsDeleted
        {
            get;
            private set;
        }

        public Uri Url
        {
            get;
            private set;
        }

        public string Title
        {
            get;
            private set;
        }

        public Uri ThumbnailUri
        {
            get;
            private set;
        }

        public string Location
        {
            get;
            private set;
        }

        public string ShortDate
        {
            get;
            private set;
        }

        public int Price
        {
            get
            {
                return _price;
            }
            set
            {
                _price = value;
            }
        }

        public bool HasPictures
        {
            get;
            private set;
        }

        public bool HasImages
        {
            get;
            private set;
        }

        public bool HasMap
        {
            get;
            private set;
        }

        public ulong ID
        {
            get
            {
                if (this.Url != null)
                {
                    Regex idMatch = new Regex(@".*/(\d*)\.html$");
                    Match match = idMatch.Match(this.Url.ToString());
                    if (match.Success)
                    {
                        return ulong.Parse(match.Groups[1].Value);
                    }
                }
                return 0;
            }
        }
        #endregion

        #region Detail Properties
        public DateTime Timestamp
        {
            get;
            private set;
        }

        public string Email
        {
            get;
            private set;
        }

        public string Phone
        {
            get;
            private set;
        }

        public string UserHtml
        {
            get;
            private set;
        }

        public string PostText
        {
            get;
            private set;
        }

        public PostDetailStatus DetailStatus
        {
            get
            {
                lock (_detailStatusLock)
                {
                    return _detailStatus;
                }
            }
            set
            {
                lock (_detailStatusLock)
                {
                    _detailStatus = value;
                }
            }
        }

        public QueryResult QueryResult
        {
            get;
            private set;
        }

        public string CategoryCode
        {
            get;
            private set;
        }

        public List<Uri> Images
        {
            get;
            private set;
        }

        public List<Uri> Pictures
        {
            get;
            set;
        }

        public Coordinate MapCoordinate
        {
            get;
            private set;
        }
        #endregion

        #region Fields
        int _price;
        object _detailStatusLock;
        volatile PostDetailStatus _detailStatus;
        #endregion

        #region Constants
        public enum PostDetailStatus
        {
            NotLoaded,
            Loading,
            Loaded,
            Failed,
        }
        #endregion

        public struct Coordinate
        {
            public double Latitude;
            public double Longitude;
        }
    }
}
