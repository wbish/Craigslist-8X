using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;

using WB.SDK.Logging;

using HtmlAgilityPack;

namespace WB.CraigslistApi
{
    public class Craigslist : IDisposable
    {
        #region Constructor
        static Craigslist()
        {
            _instanceLock = new object();
        }

        private Craigslist()
        {
            StorageFile file = ApplicationData.Current.TemporaryFolder.CreateFileAsync("CraigslistApi.log", CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult();
        }
        #endregion

        #region IDisposable
        bool _disposed;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {

                }

                _disposed = true;
            }
        }
        #endregion

        #region Singleton
        public static Craigslist Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new Craigslist();

                    return _instance;
                }
            }
        }

        static Craigslist _instance;
        static object _instanceLock;
        #endregion

        public async Task<CraigCityList> GetCities()
        {
            return await Task<CraigCityList>.Run(() => Geography.ScrapeLocations());
        }

        public async Task<CraigCityList> FindNearbyCities(CraigCityList cities, CraigCity city)
        {
            if (cities == null)
            {
                throw new CraigCityListNotCachedException();
            }

            CraigCityList list = await Task<CraigCityList>.Run(() => Geography.FindNearbyCities(cities, city));
            return list;
        }

        public async Task<CraigCity> ResolveCity(CraigCityList cities)
        {
            if (cities == null)
            {
                throw new CraigCityListNotCachedException();
            }

            Uri location = await Task<Uri>.Run(() => Geography.ResolveLocation());

            // Sometimes geo.craigslist.org is not able to resolve the location.
            if (location == null)
                return null;

            CraigCity city = cities.GetCityByUri(location);
            await Logger.AssertNotNull(city, "failed to find city by url");
            return city;
        }

        public async Task<CategoryList> GetCategories()
        {
            return await Task<CategoryList>.Run(() => Categories.ScrapeCategories());
        }

        public async Task<Uri> GetPostUri(CraigCity city)
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(city.Location))
            {
                if (response.IsSuccessStatusCode)
                {
                    HtmlDocument html = new HtmlDocument();
                    html.LoadHtml(await response.Content.ReadAsStringAsync());

                    HtmlNode postLink = (from ul in html.DocumentNode.Descendants("ul").Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == "postlks")
                                         from link in ul.Descendants("a").Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == "post")
                                         select link).FirstOrDefault();

                    if (postLink != null)
                    {
                        return new Uri(postLink.Attributes["href"].Value);
                    }
                }
                else
                {
                    Logger.LogMessage("CraigslistApi", "Unsuccessful status code '{0}' returned when sending request to Craigslist.", response.StatusCode);
                }

                return null;
            }
        }

        public async Task<bool> Login(HttpClientHandler handler, HttpClient client, string user, string password)
        {
            const string LoginUrl = "https://accounts.craigslist.org/login";

            try
            {
                using (HttpContent content = new StringContent(string.Format("inputEmailHandle={0}&inputPassword={1}", Uri.EscapeDataString(user), password)))
                {
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    using (HttpResponseMessage response = await client.PostAsync(LoginUrl, content))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            CookieCollection cookies = handler.CookieContainer.GetCookies(response.RequestMessage.RequestUri);

                            foreach (Cookie c in cookies)
                            {
                                if (c.Name == "cl_session" && !string.IsNullOrEmpty(c.Value))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return false;
        }

        public async Task<bool> FlagPost(Post post, FlagCode code)
        {
            const string FlagUrl = "http://{0}/flag/?flagCode={1}&postingID={2}";

            try
            {

                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(string.Format(FlagUrl, post.Url.Host, post.ID, code)))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return false;
        }

        public async Task<bool> ValidateCreds(string user, string password)
        {
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler())
                {
                    handler.UseCookies = true;
                    handler.CookieContainer = new CookieContainer();

                    using (HttpClient client = new HttpClient(handler, disposeHandler: true))
                    {
                        return await Login(handler, client, user, password);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            return false;
        }

        #region Constants
        internal const string GeoUrl = "http://geo.craigslist.org/";
        internal const string SitesUrl = "http://www.craigslist.org/about/sites/";
        internal const string CategoryUrl = "http://seattle.craigslist.org/";
        internal const string SubAreasUrl = "http://www.craigslist.org/about/bulk_posting_interface";
        #endregion
    }

    public class CraigCityListNotCachedException : Exception
    {
        public CraigCityListNotCachedException()
            : base("Must call CacheCities() before calling this method")
        {
        }
    }

    public enum FlagCode : byte
    {
        Miscategorized = 16,
        Prohibited = 28,
        Spam = 15,
        BestOf = 9,
    }
}
