using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;

using WB.SDK;
using WB.SDK.Logging;

namespace WB.CraigslistApi
{
    public static class QueryFilterExtensions
    {
        public static bool ListItemsEqual<T>(this List<T> source, List<T> target) where T : IEquatable<T>
        {
            // If both are null that is ok, but if only one of the lists is null.
            if (source == null && target == null)
                return true;

            if (source == null || target == null)
                return false;

            foreach (var filter in source)
            {
                if (!target.Contains(filter))
                    return false;
            }

            return true;
        }

        public static List<T> CloneList<T>(this List<T> source) where T : ICloneable<T>
        {
            if (source == null)
                throw new ArgumentNullException("source");

            return (from x in source select x.Clone()).ToList();
        }
    }

    public abstract class QueryFilter : ICloneable<QueryFilter>, IEquatable<QueryFilter>
    {
        public static QueryFilters GetFilters(Category category)
        {
            QueryFilters filters = new QueryFilters();

            switch (category.Root)
            {
                case "community":
                case "discussion forums":
                case "resumes":
                case "services":
                    break; // no filters
                case "personals":
                    switch (category.Name)
                    {
                        case "rants and raves":
                            break;
                        default:
                            filters.Add(new QueryFilterNumeric("Minimum Age", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Age", "maxAsk"));
                            break;
                    }
                    break;
                case "housing":
                    switch (category.Name)
                    {
                        case "rooms / shared":
                            filters.Add(new QueryFilterNumeric("Minimum Rent", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Rent", "maxAsk"));
                            filters.Add(new QueryFilterBoolean("Cats", "addTwo", "purrr", null));
                            filters.Add(new QueryFilterBoolean("Dogs", "addThree", "wooof", null));
                            break;
                        case "parking / storage":
                            filters.Add(new QueryFilterNumeric("Minimum Rent", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Rent", "maxAsk"));
                            break;
                        case "office / commercial":
                            filters.Add(new QueryFilterNumeric("Minimum Rent", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Rent", "maxAsk"));
                            filters.Add(new QueryFilterNumeric("Minimum Sq. Ft.", "minSqft"));
                            filters.Add(new QueryFilterNumeric("Maximum Sq. Ft.", "maxSqft"));
                            break;
                        case "real estate for sale":
                            filters.Add(new QueryFilterNumeric("Minimum Price", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Price", "maxAsk"));
                            filters.Add(new QueryFilterChooseOne("Rooms", "bedrooms",
                                new QueryFilterChooseOneItem { Label = "0+ BR", Value = null },
                                new QueryFilterChooseOneItem { Label = "1 BR", Value = "1" },
                                new QueryFilterChooseOneItem { Label = "2 BR", Value = "2" },
                                new QueryFilterChooseOneItem { Label = "3 BR", Value = "3" },
                                new QueryFilterChooseOneItem { Label = "4 BR", Value = "4" },
                                new QueryFilterChooseOneItem { Label = "5 BR", Value = "5" },
                                new QueryFilterChooseOneItem { Label = "6 BR", Value = "6" },
                                new QueryFilterChooseOneItem { Label = "7 BR", Value = "7" },
                                new QueryFilterChooseOneItem { Label = "8 BR", Value = "8" })
                                );
                            break;
                        default:
                            filters.Add(new QueryFilterNumeric("Minimum Rent", "minAsk"));
                            filters.Add(new QueryFilterNumeric("Maximum Rent", "maxAsk"));
                            filters.Add(new QueryFilterBoolean("Cats", "addTwo", "purrr", null));
                            filters.Add(new QueryFilterBoolean("Dogs", "addThree", "wooof", null));
                            filters.Add(new QueryFilterChooseOne("Rooms", "bedrooms",
                                new QueryFilterChooseOneItem { Label = "0+ BR", Value = null },
                                new QueryFilterChooseOneItem { Label = "1 BR", Value = "1" },
                                new QueryFilterChooseOneItem { Label = "2 BR", Value = "2" },
                                new QueryFilterChooseOneItem { Label = "3 BR", Value = "3" },
                                new QueryFilterChooseOneItem { Label = "4 BR", Value = "4" },
                                new QueryFilterChooseOneItem { Label = "5 BR", Value = "5" },
                                new QueryFilterChooseOneItem { Label = "6 BR", Value = "6" },
                                new QueryFilterChooseOneItem { Label = "7 BR", Value = "7" },
                                new QueryFilterChooseOneItem { Label = "8 BR", Value = "8" })
                                );
                            break;
                    }
                    break;
                case "for sale":
                    filters.Add(new QueryFilterNumeric("Minimum Price", "minAsk"));
                    filters.Add(new QueryFilterNumeric("Maximum Price", "maxAsk"));
                    break;
                case "jobs":
                    filters.Add(new QueryFilterBoolean("Telecommute", "addOne", "telecommute", null));
                    filters.Add(new QueryFilterBoolean("Contract", "addTwo", "contract", null));
                    filters.Add(new QueryFilterBoolean("Internship", "addThree", "internship", null));
                    filters.Add(new QueryFilterBoolean("Part-time", "addFour", "part-time", null));
                    filters.Add(new QueryFilterBoolean("Non-profit", "addFive", "non-profit", null));
                    break;
                case "gigs":
                    filters.Add(new QueryFilterChooseOne("Payed", "addThree",
                        new QueryFilterChooseOneItem { Label = "All", Value = string.Empty },
                        new QueryFilterChooseOneItem { Label = "Yes", Value = "forpay" },
                        new QueryFilterChooseOneItem { Label = "No", Value = "nopay" })
                        );
                    break;
                default:
                    Logger.AssertNotReached("Unable to get filter list for category");
                    break;
            }

            return filters;
        }

        public static string Serialize(QueryFilter filter)
        {
            if (filter is QueryFilterBoolean)
            {
                QueryFilterBoolean qb = (QueryFilterBoolean)filter;
                return string.Format("<qb><l>{0}</l><qf>{1}></qf><qvt>{2}</qvt><qvf>{3}</qvf><s>{4}</s></qb>",
                    qb.Label,
                    qb.QueryField,
                    qb.TrueValue,
                    qb.FalseValue,
                    qb.Selected
                    );
            }
            else if (filter is QueryFilterNumeric)
            {
                QueryFilterNumeric qn = (QueryFilterNumeric)filter;
                return string.Format("<qn><l>{0}</l><qf>{1}</qf><v>{2}</v></qn>",
                    qn.Label,
                    qn.QueryField,
                    qn.Value
                    );
            }
            else if (filter is QueryFilterChooseOne)
            {
                QueryFilterChooseOne qc = (QueryFilterChooseOne)filter;
                string values = string.Empty;
                foreach (var qco in qc.Values)
                {
                    values += string.Format(@"<vi sel=""{0}""><l>{1}</l><v>{2}</v></vi>", qc.Selected == qco, qco.Label, qco.Value);
                }

                return string.Format("<qc><l>{0}</l><qf>{1}</qf><vs>{2}</vs></qc>",
                    qc.Label,
                    qc.QueryField,
                    values
                    );
            }
            else
            {
                Logger.AssertNotReached("Unrecognized filter type");
            }

            return null;
        }

        public static QueryFilter Deserialize(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlElement xe = doc.DocumentElement;

            if (xe.NodeName == "qn")
            {
                QueryFilterNumeric qn = new QueryFilterNumeric(xe.SelectSingleNode("l").InnerText, 
                    xe.SelectSingleNode("qf").InnerText);

                qn.Value = xe.SelectSingleNode("v").InnerText;
                return qn;
            }
            else if (xe.NodeName == "qb")
            {
                QueryFilterBoolean qb = new QueryFilterBoolean(xe.SelectSingleNode("l").InnerText, 
                    xe.SelectSingleNode("qf").InnerText, 
                    xe.SelectSingleNode("qvt").InnerText, 
                    xe.SelectSingleNode("qvf").InnerText);

                qb.Selected = bool.Parse(xe.SelectSingleNode("s").InnerText);
                return qb;
            }
            else if (xe.NodeName == "qc")
            {
                QueryFilterChooseOneItem selected = null;
                List<QueryFilterChooseOneItem> values = new List<QueryFilterChooseOneItem>();
                XmlNodeList vi = xe.SelectNodes("vs/vi");
                foreach (var node in vi)
                {
                    bool sel = false;
                    values.Add(new QueryFilterChooseOneItem() { Label = node.SelectSingleNode("l").InnerText, Value = node.SelectSingleNode("v").InnerText });
                    if (bool.TryParse(node.Attributes[0].InnerText, out sel) && sel)
                    {
                        selected = values[values.Count - 1];
                    }
                }
                
                QueryFilterChooseOne co = new QueryFilterChooseOne(xe.SelectSingleNode("l").InnerText,
                    xe.SelectSingleNode("qf").InnerText,
                    values.ToArray());

                if (selected != null)
                    co.Selected = selected;

                return co;
            }
            else
            {
                Logger.AssertNotReached("Unrecognized filter type");
            }
            
            return null;
        }

        public virtual bool Equals(QueryFilter o)
        {
            if (this.GetType() != o.GetType())
                return false;

            bool equals = true;
            equals &= this.Type == o.Type;
            equals &= this.Label == o.Label;
            equals &= this.QueryField == o.QueryField;

            return equals;
        }

        public abstract QueryFilter Clone();

        public abstract string GetQueryField();

        public FilterType Type
        {
            get;
            protected set;
        }

        public string Label
        {
            get;
            protected set;
        }

        public string QueryField
        {
            get;
            protected set;
        }

        public enum FilterType
        {
            Numeric,
            Boolean,
            ChooseOne,
        }
    }

    public class QueryFilters : List<QueryFilter>
    {
        public QueryFilters()
        {
        }

        public QueryFilters(IEnumerable<QueryFilter> filters)
            : this()
        {
            if (filters != null)
            {
                foreach (var f in filters)
                {
                    this.Add(f);
                }
            }
        }

        public static string Serialize(QueryFilters qf)
        {
            if (qf == null)
                return null;

            StringBuilder sb = new StringBuilder();

            foreach (var filter in qf)
            {
                sb.AppendLine(QueryFilter.Serialize(filter));
            }

            return string.Format("<qf>{0}</qf>", sb.ToString());
        }

        public static QueryFilters Deserialize(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                return null;

            QueryFilters qf = new QueryFilters();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            if (doc.FirstChild.NodeName == "qf")
            {
                XmlNodeList nodes = doc.SelectNodes("qf/qn | qf/qb | qf/qc");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        qf.Add(QueryFilter.Deserialize(node.GetXml()));
                    }
                }

                return qf;
            }
            else
            {
                Logger.AssertNotReached("Unknown xml element");
            }

            return null;
        }

        public bool FiltersSet()
        {
            foreach (var filter in this)
            {
                if (filter is QueryFilterNumeric && !string.IsNullOrEmpty(((QueryFilterNumeric)filter).Value))
                    return true;
                else if (filter is QueryFilterBoolean && ((QueryFilterBoolean)filter).Selected)
                    return true;
                else if (filter is QueryFilterChooseOne && ((QueryFilterChooseOne)filter).Selected != ((QueryFilterChooseOne)filter).Values[0])
                    return true;
            }

            return false;
        }
    }

    public class QueryFilterNumeric : QueryFilter
    {
        public QueryFilterNumeric(string label, string qf)
        {
            this.Type = QueryFilter.FilterType.Numeric;
            this.Label = label;
            this.QueryField = qf;
        }

        public override bool Equals(QueryFilter o)
        {
            bool equals = base.Equals(o);

            if (!equals)
                return false;

            equals &= this.Value == (o as QueryFilterNumeric).Value;
            return equals;
        }

        public override QueryFilter Clone()
        {
            return new QueryFilterNumeric(this.Label, this.QueryField) { Value = this.Value };
        }

        public override string GetQueryField()
        {
            return string.Format("{0}={1}", this.QueryField, this.Value);
        }

        public string Value
        {
            get;
            set;
        }
    }

    public class QueryFilterBoolean : QueryFilter
    {
        public QueryFilterBoolean(string label, string qf, string qvt, string qvf)
        {
            this.Type = QueryFilter.FilterType.Boolean;
            this.Label = label;
            this.QueryField = qf;
            this._qvt = qvt;
            this._qvf = qvf;
        }

        public override bool Equals(QueryFilter o)
        {
            bool equals = base.Equals(o);

            if (!equals)
                return false;

            equals &= this.Selected == (o as QueryFilterBoolean).Selected;
            return equals;
        }

        public override QueryFilter Clone()
        {
            return new QueryFilterBoolean(this.Label, this.QueryField, this._qvt, this._qvf) { Selected = this.Selected };
        }

        public override string GetQueryField()
        {
            return string.Format("{0}={1}", this.QueryField, this.Selected ? this._qvt : this._qvf);
        }

        public bool Selected
        {
            get;
            set;
        }

        public string TrueValue
        {
            get
            {
                return this._qvt;
            }
        }

        public string FalseValue
        {
            get
            {
                return this._qvf;
            }
        }

        string _qvt;
        string _qvf;
    }

    public class QueryFilterChooseOne : QueryFilter
    {
        public QueryFilterChooseOne(string label, string qf, params QueryFilterChooseOneItem[] values)
        {
            this.Type = FilterType.ChooseOne;
            this.Label = label;
            this.QueryField = qf;

            Logger.Assert(values.Length > 2, "Choose one filter implies multiple choices possible");

            Values = new List<QueryFilterChooseOneItem>(values);
            Selected = Values[0];
        }

        public override bool Equals(QueryFilter o)
        {
            bool equals = base.Equals(o);

            if (!equals)
                return false;

            equals &= this.Selected.Equals((o as QueryFilterChooseOne).Selected);
            equals &= this.Values.ListItemsEqual((o as QueryFilterChooseOne).Values);
            return equals;
        }

        public override QueryFilter Clone()
        {
            var values = (from x in this.Values select x.Clone()).ToList();
            int index = values.IndexOf(this.Selected);
            QueryFilterChooseOne item = new QueryFilterChooseOne(this.Label, this.QueryField, values.ToArray());

            if (index >= 0)
                item.Selected = item.Values[index];

            return item;
        }

        public override string GetQueryField()
        {
            return string.Format("{0}={1}", this.QueryField, Selected.Value);
        }

        public List<QueryFilterChooseOneItem> Values { get; set; }
        public QueryFilterChooseOneItem Selected { get; set; }
    }

    public class QueryFilterChooseOneItem : ICloneable<QueryFilterChooseOneItem>, IEquatable<QueryFilterChooseOneItem>, IComparable<QueryFilterChooseOneItem>
    {
        public QueryFilterChooseOneItem Clone()
        {
            return new QueryFilterChooseOneItem() { Label = this.Label, Value = this.Value };
        }

        public bool Equals(QueryFilterChooseOneItem o)
        {
            return this.CompareTo(o) == 0;
        }

        public int CompareTo(QueryFilterChooseOneItem o)
        {
            int x = this.Label.CompareTo(o.Label);
            if (x != 0)
                return x;

            if (string.IsNullOrEmpty(this.Value) && string.IsNullOrEmpty(o.Value))
            {
                return 0;
            }
            else if (this.Value == null)
            {
                return 1; // o is bigger
            }
            else if (o.Value == null)
            {
                return -1;
            }

            return this.Value.CompareTo(o.Value);
        }

        public string Label
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }
    }
}