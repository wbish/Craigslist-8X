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
    public class Query : ICloneable<Query>, IEquatable<Query>
    {
        #region Initialization
        public Query(CraigCity city, Category category, string query)
            : this(city, category, query, null)
        {
        }

        public Query(CraigCity city, Category category, string query, QueryFilters filters)
        {
            if (city == null)
                throw new ArgumentNullException("city");
            if (category == null)
                throw new ArgumentNullException("category");

            this.City = city;
            this.Category = category;
            this.Text = query ?? string.Empty;
            this.HasImage = false;
            this.Type = QueryType.EntirePost;
            this.Filters = filters;
        }
        #endregion

        public bool Equals(Query q)
        {
            bool equal = true;

            equal &= this.City.Equals(q.City);
            equal &= this.Category.Equals(q.Category);
            equal &= this.Text.Equals(q.Text);
            equal &= this.HasImage == q.HasImage;
            equal &= this.Type == q.Type;
            equal &= ((this.Filters != null && q.Filters != null) || this.Filters == null && q.Filters == null);

            if (equal)
                equal &= this.Filters.ListItemsEqual(q.Filters);

            return equal;
        }

        public Query Clone()
        {
            Query q = new Query(this.City.Clone(), this.Category.Clone(), this.Text);
            q.HasImage = this.HasImage;
            q.Type = this.Type;
            q.Sort = this.Sort;

            if (this.Filters != null)
                q.Filters = new QueryFilters(this.Filters.CloneList());

            return q;
        }

        public async Task<QueryResult> Execute(CancellationToken token)
        {
            ObservableCollection<Post> posts = new ObservableCollection<Post>();
            QueryResult qr = null;
            Uri uri = await this.ConstructUri();

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);

                    using (HttpResponseMessage response = await client.GetAsync(uri, token))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            qr = new QueryResult(this, posts, response);
                            await QueryResult.ParseRows(token, response, qr);
                        }
                        else
                        {
                            Logger.LogMessage("CraigslistApi", "Unsuccessful status code '{0}' returned when sending request to Craigslist.", response.StatusCode);
                            return null;
                        }
                    }
                }

                return qr;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogMessage("CraigslistApi", "Exception thrown when sending http request to craigslist.");
                Logger.LogException(ex);
                return null;
            }
        }

        public Uri GetQueryUrl()
        {
            return this.ConstructUri().Result;
        }

        private async Task<Uri> ConstructUri()
        {
            return await ConstructUri(0);
        }

        internal async Task<Uri> ConstructUri(int itemsLoaded)
        {
            if (this.Mode == SearchMode.Query || this.Filters != null || this.HasImage)
            {
                // Construct the URI
                string domain = string.Format("{0}search/", City.Location.AbsoluteUri);
                string path = Category.Abbreviation;
                if (!string.IsNullOrWhiteSpace(City.SubArea))
                    path += string.Format("/{0}", City.SubArea);

                // Attach query string
                string query = string.Format("?query={0}&srchType={1}", this.Text, this.Type == QueryType.EntirePost ? "A" : "T");

                if (this.Filters != null)
                {
                    foreach (var filter in Filters)
                    {
                        query += "&" + filter.GetQueryField();
                    }
                }

                if (this.HasImage)
                {
                    query += "&hasPic=1";
                }

                if (itemsLoaded > 0)
                {
                    query += string.Format("&s={0}", itemsLoaded);
                }

                if (this.Sort != SortOrder.Recent)
                {
                    string value = string.Empty;

                    switch (this.Sort)
                    {
                        case SortOrder.Match:
                            value = "rel";
                            break;
                        case SortOrder.LowPrice:
                            value = "priceasc";
                            break;
                        case SortOrder.HighPrice:
                            value = "pricedsc";
                            break;
                        default:
                            await Logger.AssertNotReached("Unexpected sort order value");
                            break;
                    }

                    query += string.Format("&sort={0}", value);
                }

                return new Uri(domain + path + query);
            }
            else
            {
                // Just browsing
                await Logger.Assert(this.Mode == SearchMode.Browsing, "We should only construct this type of Url in browsing mode");
                await Logger.Assert(itemsLoaded % 100 == 0, "Results sets are in sets of 100");

                string location = City.IsSubArea ? (City.SubLocation.AbsoluteUri + "/") : City.Location.AbsoluteUri;
                string catAbbr = string.Format("{0}/", this.Category.Abbreviation);
                string index = string.Empty;

                if (itemsLoaded > 0)
                {
                    index = string.Format("index{0}.html", itemsLoaded);
                }

                return new Uri(location + catAbbr + index);
            }
        }

        public static string Serialize(Query q)
        {
            return string.Format("<q><city>{0}</city><cat>{1}</cat><qt>{2}</qt><i>{3}</i><t>{4}</t>{5}</q>",
                q.City.ToString(),
                q.Category.ToString(),
                q.Text,
                q.HasImage,
                q.Type,
                QueryFilters.Serialize(q.Filters) ?? string.Empty);
        }

        public static Query Deserialize(string q)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(q);

            CraigCity city = CraigCity.Deserialize(doc.SelectSingleNode("q/city").ChildNodes[0].NodeValue.ToString());
            Category cat = Category.Deserialize(doc.SelectSingleNode("q/cat").ChildNodes[0].NodeValue.ToString());

            string query = null;
            IXmlNode qtNode = doc.SelectSingleNode("q/qt");
            if (qtNode.ChildNodes.Count == 1)
                query = qtNode.ChildNodes[0].NodeValue.ToString();

            bool hasImage = false;
            IXmlNode i = doc.SelectSingleNode("q/i");
            if (i != null && i.ChildNodes.Count > 0 && i.ChildNodes[0].NodeValue != null)
            {
                bool.TryParse(i.ChildNodes[0].NodeValue.ToString(), out hasImage);
            }

            QueryType type = QueryType.EntirePost;
            IXmlNode t = doc.SelectSingleNode("q/t");
            if (t != null && t.ChildNodes.Count > 0 && t.ChildNodes[0].NodeValue != null)
            {
                Enum.TryParse(t.ChildNodes[0].NodeValue.ToString(), out type);
            }

            QueryFilters filters = null;
            IXmlNode qf = doc.SelectSingleNode("q/qf");
            if (qf != null)
            {
                filters = QueryFilters.Deserialize(qf.GetXml());
            }

            Query qo = new Query(city, cat, query);
            qo.HasImage = hasImage;
            qo.Type = type;
            qo.Filters = filters;

            return qo;
        }

        #region Properties
        public CraigCity City
        {
            get;
            set;
        }

        public Category Category
        {
            get;
            private set;
        }

        public string Text
        {
            get;
            private set;
        }

        public bool HasImage
        {
            get;
            set;
        }

        public QueryType Type
        {
            get;
            set;
        }

        public QueryFilters Filters
        {
            get;
            private set;
        }

        public SearchMode Mode
        {
            get
            {
                return string.IsNullOrWhiteSpace(this.Text) ? SearchMode.Browsing : SearchMode.Query;
            }
        }

        public SortOrder Sort
        {
            get;
            set;
        }
        #endregion

        #region Constants
        public enum QueryType
        {
            TitleOnly,
            EntirePost,
        }

        public enum SearchMode
        {
            Query,
            Browsing,
        }

        public enum SortOrder
        {
            Recent,
            Match,
            LowPrice,
            HighPrice,
        }
        #endregion
    }
}