using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;

using HtmlAgilityPack;

using WB.SDK;
using WB.SDK.Logging;

namespace WB.CraigslistApi
{
    internal static class Geography
    {
        internal static async Task<CraigCityList> FindNearbyCities(CraigCityList all, CraigCity city)
        {
            if (city == null)
                throw new ArgumentNullException("city");
            if (all == null)
                throw new ArgumentNullException("all");

            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(city.Location))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        CraigCityList cities = new CraigCityList();
                        HtmlDocument html = new HtmlDocument();
                        html.LoadHtml(await response.Content.ReadAsStringAsync());

                        throw new NotImplementedException();

                        //IXmlNode node = xml.SelectSingleNode("//html/body/table/tr/td/ul/li/h5[@class=\"ban\"]");

                        //Logger.Assert(node != null, "banner node is null");
                        //if (node == null)
                        //    return null;

                        //Logger.Assert(node.GetXml().Contains("nearby"), "did not find nearby node");

                        //XmlElement ul = node.NextSibling as XmlElement;
                        //XmlNodeList items = ul.SelectNodes("li[@class=\"s\"]");

                        //foreach (var item in items)
                        //{
                        //    Uri uri = new Uri(Uri.UnescapeDataString(item.NextSibling.Attributes[0].NodeValue.ToString()));
                        //    cities.Add(all.GetCityByUri(uri));
                        //}

                        //return cities;
                    }

                    return null;
                }
            }
            catch (HttpRequestException)
            {
                // TODO: Log exception
                throw;
            }
        }

        internal static Uri ResolveLocation()
        {
            using (HttpResponseMessage response = Utilities.ExecuteRetryable<HttpResponseMessage>(() => GetResolveLocationResponse(), 3))
            {
                if (response != null)
                {
                    return response.RequestMessage.RequestUri;
                }
            }

            return null;
        }

        private static HttpResponseMessage GetResolveLocationResponse()
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync(Craigslist.GeoUrl).GetAwaiter().GetResult();

                // Sometimes Craigslist is unable to resolve the correct location so we just need to handle that case.
                if (response.IsSuccessStatusCode && !response.RequestMessage.RequestUri.AbsoluteUri.Equals(Craigslist.SitesUrl, StringComparison.OrdinalIgnoreCase))
                    return response;
                else
                    throw new RetryException();
            }
        }

        internal static async Task<CraigCityList> ScrapeLocations()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(Craigslist.SitesUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        HtmlDocument html = new HtmlDocument();
                        html.LoadHtml(await response.Content.ReadAsStringAsync());

                        CraigCityList cities = new CraigCityList();

                        return null;

                        //XmlNodeList continents = document.SelectNodes(@"//h1[@class=""continent_header""]");
                        //foreach (var coNode in continents)
                        //{
                        //    string continent = string.Empty;

                        //    // For some reason the OCEANIA continent is not formatted as the rest
                        //    if (coNode.ChildNodes.Count > 1)
                        //        continent = coNode.ChildNodes[1].ChildNodes[0].NodeValue as string;
                        //    else
                        //        continent = coNode.ChildNodes[0].ChildNodes[0].ChildNodes[0].NodeValue as string;

                        //    foreach (var boxNode in coNode.SelectNodes(@"../div/div/div/div"))
                        //    {
                        //        foreach (var sNode in boxNode.SelectNodes(@"div[@class=""state_delimiter""]"))
                        //        {
                        //            if (sNode.ChildNodes.Count != 1)
                        //                continue;

                        //            string state = sNode.ChildNodes[0].ChildNodes[0].NodeValue as string;

                        //            foreach (var ciNode in sNode.NextSibling.SelectNodes(@"li/a"))
                        //            {
                        //                Uri uri = new Uri(Uri.UnescapeDataString(ciNode.Attributes[0].NodeValue as string));
                        //                string city = ciNode.ChildNodes[0].ChildNodes[0].NodeValue as string;

                        //                CraigCity craigCity = new CraigCity(uri, Uri.UnescapeDataString(continent), Uri.UnescapeDataString(state), Uri.UnescapeDataString(city));
                        //                cities.Add(craigCity);
                        //            }
                        //        }
                        //    }
                        //}

                        //await AddSubAreas(cities);

                        //return cities;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.LogException(ex);
            }

            return null;
        }

        #region Private Methods
        private static async Task AddSubAreas(CraigCityList cl)
        {
            using (HttpClient client = new HttpClient())
            {
                client.MaxResponseContentBufferSize = 256 * 1024 * 1024; // 256k

                using (HttpResponseMessage response = await client.GetAsync(Craigslist.SubAreasUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        HtmlDocument html = new HtmlDocument();
                        html.LoadHtml(await response.Content.ReadAsStringAsync());

                        throw new NotImplementedException();

                        //XmlDocument document = html.ToXmlDocument();

                        //IXmlNode h3 = document.SelectSingleNode("//html/body/h3/text[text()='Areas%20and%20Subareas%3A']");
                        //XmlNodeList nodes = h3.ParentNode.NextSibling.SelectNodes("table/tr");

                        //foreach (var row in nodes)
                        //{
                        //    // skip the headers
                        //    if (row.ChildNodes[0].NodeName == "th")
                        //        continue;

                        //    string cityName = Uri.UnescapeDataString(row.ChildNodes[1].ChildNodes[0].ChildNodes[0].GetXml()).Trim();
                        //    string subArea = Uri.UnescapeDataString(row.ChildNodes[2].ChildNodes[0].ChildNodes[0].GetXml()).Trim();
                        //    string subAreaName = Uri.UnescapeDataString(row.ChildNodes[3].ChildNodes[0].ChildNodes[0].GetXml()).Trim();

                        //    if (subArea != "&nbsp;")
                        //    {
                        //        CraigCity city = cl.GetCityByName(cityName);

                        //        if (city == null)
                        //        {

                        //            if (cityName.Contains(","))
                        //                cityName = cityName.Split(',')[0].Trim();

                        //            city = cl.GetCityByName(cityName);

                        //            Logger.AssertNotNull(city, "failed to find city by name");
                        //            if (city == null)
                        //                continue;
                        //        }

                        //        // Example:
                        //        // Location: http://seattle.craigslist.com/est
                        //        // Continent: US
                        //        // State: Washington
                        //        // City: seattle-tacoma
                        //        // Sub Area: est
                        //        // Sub Area Name: eastside
                        //        string format = city.Location.AbsoluteUri.EndsWith("/") ? "{0}{1}": "{0}/{1}";

                        //        cl.Add(new CraigCity(new Uri(city.Location.AbsoluteUri),
                        //            city.Continent,
                        //            city.State,
                        //            city.City,
                        //            new Uri(string.Format(format, city.Location.AbsoluteUri, subArea)),
                        //            subArea,
                        //            subAreaName));
                        //    }
                        //}
                    }
                }
            }
        }
        #endregion
    }
}