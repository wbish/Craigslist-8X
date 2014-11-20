using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;

namespace WB.Craigslist8X.Model
{
    public class SavedQuery : BindableBase
    {
        public SavedQuery(Query query, string name)
        {
            this.Query = query;
            this.Name = name;
            this.TileId = Guid.NewGuid();
            this.CacheDate = DateTime.MinValue;
            this.RssNewDate = DateTime.MinValue;
        }

        public static string Serialize(SavedQuery query)
        {
            if (query == null)
                return null;

            return string.Format(@"<sq name=""{0}"" tile=""{1}"" url=""{3}"" time=""{4}"">{2}</sq>",
                    Uri.EscapeDataString(query.Name),
                    query.TileId,
                    Query.Serialize(query.Query),
                    Uri.EscapeDataString(query.Query.GetQueryUrl().AbsoluteUri),
                    query.CacheDate
                );
        }

        public static SavedQuery Deserialize(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                return null;

            SavedQuery sq = null;
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlElement xe = doc.DocumentElement;
            if (xe.NodeName == "sq")
            {
                sq = new SavedQuery(Query.Deserialize(xe.SelectSingleNode("q").GetXml()), Uri.UnescapeDataString(xe.GetAttribute("name")));

                if (!string.IsNullOrEmpty(xe.GetAttribute("tile")))
                {
                    sq.TileId = Guid.Parse(xe.GetAttribute("tile"));
                }

                if (!string.IsNullOrEmpty(xe.GetAttribute("url")))
                {
                    sq.QueryUrl = new Uri(Uri.UnescapeDataString(xe.GetAttribute("url")));
                }

                if (!string.IsNullOrEmpty(xe.GetAttribute("time")))
                {
                    sq.CacheDate = DateTime.Parse(xe.GetAttribute("time"));
                }
            }

            return sq;
        }

        public Guid TileId
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public Query Query
        {
            get;
            private set;
        }

        /// <summary>
        /// In order to prevent loading our entire CraigslistApi which we are not able to from our background task binary, 
        /// we cache our target URL here.
        /// </summary>
        public Uri QueryUrl
        {
            get;
            set;
        }

        /// <summary>
        /// Cache the timestamp of the latest item we have looked at so that we now how many items are newer.
        /// </summary>
        public DateTime CacheDate
        {
            get
            {
                return this._cacheDate;
            }
            set
            {
                this.SetProperty(ref this._cacheDate, value);
            }
        }

        /// <summary>
        /// Number of new items since CacheDate
        /// </summary>
        public int Notifications
        {
            get
            {
                return this._notifications;
            }
            set
            {
                if (this.SetProperty(ref this._notifications, value))
                {
                    try
                    {
                        if (value == 0)
                        {
                            BadgeUpdater updater = BadgeUpdateManager.CreateBadgeUpdaterForSecondaryTile(this.TileId.ToString());
                            updater.Clear();
                        }
                        else
                        {
                            XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                            XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                            badgeElement.SetAttribute("value", value.ToString());
                            BadgeNotification badge = new BadgeNotification(badgeXml);
                            BadgeUpdater updater = BadgeUpdateManager.CreateBadgeUpdaterForSecondaryTile(this.TileId.ToString());
                            updater.Update(badge);
                        }
                    }
                    catch
                    {
                        // Tile does not exist
                    }
                }
            }
        }

        public DateTime RssNewDate
        {
            get;
            set;
        }

        private int _notifications;
        private DateTime _cacheDate;
    }
}