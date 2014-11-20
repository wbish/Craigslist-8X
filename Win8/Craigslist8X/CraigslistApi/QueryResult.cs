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
using WinRTXamlToolkit.Async;

using WB.SDK;
using WB.SDK.Logging;

namespace WB.CraigslistApi
{
    public class QueryResult
    {
        #region Constructor

        public QueryResult(Query query, ObservableCollection<Post> posts, HttpResponseMessage response)
        {
            this.Query = query;
            this.Posts = posts;
            this.HttpResponse = response;
            this.ItemsLock = new AsyncLock();
        }

        #endregion

        #region Methods
        public async Task LoadMoreAsync()
        {
            await Logger.Assert(!DoneLoading, "We think we are already done loading but trying to load more");
            if (DoneLoading)
                return;

            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(await this.Query.ConstructUri(this.Posts.Count)))
            {
                if (response.IsSuccessStatusCode)
                {
                    this.HttpResponse = response;
                    await QueryResult.ParseRows(new CancellationToken(), response, this);
                }
            }
        }
        #endregion

        static internal async Task ParseRows(CancellationToken token, HttpResponseMessage response, QueryResult qr)
        {
            token.ThrowIfCancellationRequested();
            await Logger.Assert(response.IsSuccessStatusCode, "We are parsing rows for an unsuccessful response!");

            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(await response.Content.ReadAsStringAsync());

            token.ThrowIfCancellationRequested();

            using (await qr.ItemsLock.LockAsync())
            {
                ParseBrowseResults(token, html, qr);
            }
        }

        static void ParseBrowseResults(CancellationToken token, HtmlDocument html, QueryResult qr)
        {
            var rows = (from x in html.DocumentNode.Descendants().Where(x => x.Name == "h4" && x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("ban")
                || x.Name == "p" && x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("row"))
                        select x).ToList();

            string shortDate = null;
            int count = 0;

            foreach (var node in rows)
            {
                if (node.Name == "h4")
                {
                    shortDate = node.InnerText;
                }
                else if (node.Name == "p")
                {
                    ++count;

                    Post p = new Post();
                    p.Parse(qr, node, shortDate);
                    qr.Posts.Add(p);

                    token.ThrowIfCancellationRequested();
                }
            }

            qr.DoneLoading = (count < 100);
            qr.Count += count;
        }

        static void ParseSearchResults(CancellationToken token, HtmlDocument html, QueryResult qr)
        {
            var rows =
                    (from p in html.DocumentNode.Descendants("p").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("row"))
                     select p).ToList();

            if (rows == null || rows.Count == 0)  // No results found
            {
                qr.Count = 0;
                qr.DoneLoading = true;
                return;
            }

            var dateBanner = (from x in html.DocumentNode.Descendants("h4").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains("ban"))
                              select x).FirstOrDefault();
            if (dateBanner != null)
            {
                string[] parts = Uri.UnescapeDataString(dateBanner.InnerText).Split(' ');
                for (int i = 0; i < parts.Length; ++i)
                {
                    int count;
                    if (int.TryParse(parts[i], out count))
                    {
                        qr.Count = count;
                        break;
                    }
                }
            }

            // If there are too few results, Craigslist might show nearby results. We probably do not want to show that here.
            if (dateBanner != null && qr.Count == 0 && rows.Count < 10)
            {
                qr.DoneLoading = true;
                return;
            }

            // Parsing each row should take approximately 0.5 milliseconds. Do this synchronously as it actually degrades perceived performance
            // to schedule this via the taskpool.
            using (MonitoredScope scope = new MonitoredScope("Parse '{0}' search rows", rows.Count))
            {
                for (int i = 0; i < rows.Count; ++i)
                {
                    Post p = new Post();
                    p.Parse(qr, rows[i], null);
                    qr.Posts.Add(p);

                    token.ThrowIfCancellationRequested();
                }
            }

            qr.DoneLoading = (qr.Posts.Count == qr.Count);
        }

        #region Properties
        public bool DoneLoading
        {
            get;
            set;
        }

        public int Count
        {
            get;
            set;
        }

        public Query Query
        {
            get;
            private set;
        }

        public ObservableCollection<Post> Posts
        {
            get;
            internal set;
        }

        public HttpResponseMessage HttpResponse
        {
            get;
            private set;
        }
        #endregion

        #region Fields
        public AsyncLock ItemsLock;
        #endregion
    }
}