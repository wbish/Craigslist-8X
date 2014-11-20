using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Notifications;

namespace Craigslist8XTasks
{
    public sealed class SearchAgentTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();

            StringBuilder notifications = new StringBuilder();
            XmlDocument doc = await LoadSavedSearches();

            if (doc != null)
            {
                // We may have saved searches. Do something.
                var savedQueries = doc.SelectNodes("root/sq");
                int globalCount = 0;

                notifications.AppendLine("<root>");

                foreach (var sq in savedQueries)
                {
                    if (sq.Attributes.GetNamedItem("url") != null)
                    {
                        Guid tile = Guid.Parse(sq.Attributes.GetNamedItem("tile").InnerText);

                        DateTime dt = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                        if (sq.Attributes.GetNamedItem("time") != null)
                            dt = DateTime.Parse(sq.Attributes.GetNamedItem("time").InnerText);

                        Uri query = new Uri(string.Format("{0}&format=rss", Uri.UnescapeDataString(sq.Attributes.GetNamedItem("url").InnerText)));

                        try
                        {
                            using (HttpClient client = new HttpClient())
                            using (var response = await client.GetAsync(query))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    string content = await response.Content.ReadAsStringAsync();

                                    XmlDocument xml = new XmlDocument();
                                    xml.LoadXml(content);

                                    var items = xml.SelectNodesNS("//ns:item", "xmlns:ns='http://purl.org/rss/1.0/'");
                                    int newItems = 0;
                                    DateTime newestItem = DateTime.MinValue;

                                    foreach (var item in items)
                                    {
                                        DateTime postTime = DateTime.Parse(item.SelectSingleNodeNS("dc:date", "xmlns:dc='http://purl.org/dc/elements/1.1/'").InnerText);
                                        if (postTime > dt)
                                        {
                                            ++newItems;
                                        }

                                        if (postTime > newestItem)
                                        {
                                            newestItem = postTime;
                                        }
                                    }

                                    notifications.AppendLine(string.Format(@"<n tile=""{0}"" count=""{1}"" time=""{2}"" />", tile, newItems, newestItem));

                                    if (newItems > 0)
                                    {
                                        globalCount += newItems;

                                        if (sq.Attributes.GetNamedItem("tile") != null)
                                        {
                                            try
                                            {
                                                XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                                                XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                                                badgeElement.SetAttribute("value", globalCount.ToString());
                                                BadgeNotification badge = new BadgeNotification(badgeXml);
                                                BadgeUpdater updater = BadgeUpdateManager.CreateBadgeUpdaterForSecondaryTile(sq.Attributes.GetNamedItem("tile").InnerText);
                                                updater.Update(badge);
                                            }
                                            catch
                                            {
                                                // Tile does not exist
                                            }
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            BadgeUpdateManager.CreateBadgeUpdaterForSecondaryTile(tile.ToString()).Clear();
                                        }
                                        catch
                                        {
                                            // Tile does not exist
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                notifications.AppendLine("</root>");
                
                // We only save to the local notifications file. Never touch the actual roaming saved searches file
                await SaveNotificationsAsync(notifications.ToString());

                if (globalCount > 0)
                {
                    try
                    {
                        XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                        XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                        badgeElement.SetAttribute("value", globalCount.ToString());
                        BadgeNotification badge = new BadgeNotification(badgeXml);
                        BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    try
                    {
                        BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                try
                {
                    BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
                }
                catch
                {
                }
            }

            deferral.Complete();
        }

        public IAsyncOperation<XmlDocument> LoadSavedSearches()
        {
            return Task.Run<XmlDocument>(async () =>
                {
                    StorageFile file = null;

                    try
                    {
                        file = await ApplicationData.Current.RoamingFolder.GetFileAsync(SavedSearchesFileName);
                    }
                    catch (FileNotFoundException)
                    {
                        return null;
                    }

                    if (file != null)
                    {
                        try
                        {
                            using (var stream = await file.OpenAsync(FileAccessMode.Read))
                            using (var readStream = stream.GetInputStreamAt(0))
                            using (DataReader reader = new DataReader(readStream))
                            {
                                uint bytesLoaded = await reader.LoadAsync((uint)stream.Size);
                                string content = reader.ReadString(bytesLoaded);

                                if (string.IsNullOrEmpty(content))
                                    return null;

                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(content);

                                return doc;
                            }
                        }
                        catch
                        {
                            System.Diagnostics.Debugger.Break();
                        }
                    }

                    return null;
                }).AsAsyncOperation();
        }

        public IAsyncAction SaveNotificationsAsync(string content)
        {
            return Task.Run(async () =>
                {
                    try
                    {
                        StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(NotificationsFileName, CreationCollisionOption.ReplaceExisting);

                        using (var raStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        using (var outStream = raStream.GetOutputStreamAt(0))
                        using (DataWriter writer = new DataWriter(outStream))
                        {
                            writer.WriteString(content);

                            await writer.StoreAsync();
                            await outStream.FlushAsync();
                        }
                    }
                    catch
                    {
                    }
                }).AsAsyncAction();
        }

        #region Constants
        const string SavedSearchesFileName = "SavedSearches.xml";
        const string NotificationsFileName = "SavedSearchesNotifications.xml";
        #endregion
    }
}
