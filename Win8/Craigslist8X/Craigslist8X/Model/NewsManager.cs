using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WB.SDK.Logging;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;

namespace WB.Craigslist8X.Model
{
    public class NewsManager
    {
        static NewsManager()
        {
            _instanceLock = new object();
        }

        private NewsManager()
        {
        }

        #region Singleton
        public static NewsManager Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new NewsManager();

                    return _instance;
                }
            }
        }

        static NewsManager _instance;
        static object _instanceLock;
        #endregion

        private async Task CacheFeed()
        {
            try
            {
                if (this._feed != null)
                    return;

                using (HttpClient client = new HttpClient())
                using (var response = await client.GetAsync(NewsfeedUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        this._feed = new XmlDocument();
                        this._feed.LoadXml(await response.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public async Task<bool> IsNewItem()
        {
            try
            {
                await this.CacheFeed();

                if (this._feed == null)
                    return false;

                DateTime start = NewsStartTime;
                var items = this._feed.SelectNodes("//item");

                foreach (var item in items)
                {
                    DateTime itemDate;
                    var date = item.SelectSingleNode("pubDate").InnerText;
                    DateTimeFormatInfo format = new DateTimeFormatInfo();

                    if (DateTime.TryParseExact(date, format.RFC1123Pattern, null, DateTimeStyles.None, out itemDate))
                    {
                        // Make sure we haven't read it before and that the news item is no more than 30 days old.
                        if (itemDate > start)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        public async Task<List<NewsItem>> GetNews()
        {
            // We only start getting items from the time we last read a news item or 30 days old, whichever is more recent.
            DateTime start = NewsStartTime;
            List<NewsItem> news = new List<NewsItem>();

            try
            {
                await this.CacheFeed();

                if (this._feed == null)
                    return null;

                DateTime lastItemRead = DateTime.Parse(Settings.Instance.LastReadNewsItem);
                var items = this._feed.SelectNodes("//item");

                foreach (var item in items)
                {
                    DateTime itemDate;
                    var date = item.SelectSingleNode("pubDate").InnerText;
                    DateTimeFormatInfo format = new DateTimeFormatInfo();

                    if (DateTime.TryParseExact(date, format.RFC1123Pattern, null, DateTimeStyles.None, out itemDate))
                    {
                        if (itemDate > start)
                        {
                            if (itemDate > lastItemRead)
                            {
                                Settings.Instance.LastReadNewsItem = itemDate.ToString();
                            }

                            NewsItem newsItem = new NewsItem();
                            newsItem.Date = itemDate.ToString("MM/dd/yyyy");
                            newsItem.Title = item.SelectSingleNode("title").InnerText;
                            newsItem.Content = item.SelectSingleNode("description").InnerText;
                            news.Add(newsItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                return null;
            }

            return news;
        }

        /// <summary>
        /// Basically this only exists because of the Windows 8 certification process. They suck. 
        /// Every once in a while you get a reviewer that simply does not understand the app is a storefront app
        /// and personals is allowed. This is an attempt to bypass that.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsPersonalsAvailable()
        {
            try
            {
                await this.CacheFeed();

                if (this._feed == null)
                    return false;

                var latest = this._feed.SelectNodesNS("//c8x:latest", "xmlns:c8x='http://www.craigslist8x.com'").FirstOrDefault();

                if (latest != null)
                {
                    Version latestAllowed;

                    if (Version.TryParse(latest.InnerText, out latestAllowed))
                    {
                        PackageVersion app = Package.Current.Id.Version;
                        Version current = new Version(string.Format("{0}.{1}.{2}.{3}", app.Major, app.Minor, app.Build, app.Revision));

                        int result = latestAllowed.CompareTo(current);

                        return result >= 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return false;
        }

        private DateTime NewsStartTime
        {
            get
            {
                // We only start getting items from the time we last read a news item or 30 days old, whichever is more recent.
                DateTime readNewsItem = DateTime.Parse(Settings.Instance.LastReadNewsItem);
                DateTime force2013 = DateTime.Parse("1/1/2013 12:00:00 AM");

                // The previous default on minvalue was not a good idea, but we can't change it at this point
                // just force the new minimum to start of 2013.
                readNewsItem = readNewsItem < force2013 ? force2013 : readNewsItem;

                return DateTime.FromFileTime(Math.Max(readNewsItem.ToFileTime(), DateTime.Now.AddDays(-30).ToFileTime()));
            }
        }

        private Uri NewsfeedUrl
        {
            get
            {
#if CRAIGSLIST8XPRO
                return new Uri("http://wbishop.azurewebsites.net/rss/craigslist8xpro.xml");
#else
                return new Uri("http://wbishop.azurewebsites.net/rss/craigslist8x.xml");
#endif
            }
        }

        XmlDocument _feed;
    }

    public class NewsItem
    {
        public string Date
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public string Content
        {
            get;
            set;
        }
    }
}
