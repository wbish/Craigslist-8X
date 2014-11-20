using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;

using HtmlAgilityPack;

using WB.SDK;
using WB.SDK.Logging;
using WB.SDK.Parsing;

namespace WB.CraigslistApi
{
    internal static class Categories
    {
        internal static async Task<CategoryList> ScrapeCategories()
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(new Uri(Craigslist.CategoryUrl)))
            {
                try
                {
                    if (response.IsSuccessStatusCode)
                    {
                        CategoryList cl = new CategoryList();
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(await response.Content.ReadAsStringAsync());

                        var nodes = (from h in doc.DocumentNode.Descendants("h4").Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == "ban") select h);

                        foreach (var node in nodes)
                        {
                            string root;

                            if (node.Name == "a")
                            {
                                root = Uri.UnescapeDataString(node.InnerText);
                                string name = string.Format("all {0}", root);
                                string href = Uri.UnescapeDataString(node.Attributes["href"].Value);

                                cl.Add(new Category(root, name, href));
                            }
                            else
                            {
                                root = Uri.EscapeDataString(node.InnerText);
                            }

                            if (node.NextSibling == null)
                                continue;

                            foreach (var cats in node.NextSibling.Descendants("a").Where(x => x.ParentNode.Name == "li"))
                            {
                                string name = Uri.UnescapeDataString(cats.InnerText);
                                string href = Uri.UnescapeDataString(cats.Attributes["href"].Value);

                                cl.Add(new Category(root, name, href));
                            }
                        }

                        AddSubAreas(cl);

                        return cl;
                    }
                }
                catch (Exception)
                {
                }

                return null;
            }
        }

        /// <summary>
        /// TODO: Automate retrieving this
        /// </summary>
        /// <param name="categories"></param>
        private static void AddSubAreas(CategoryList categories)
        {

        }
    }

    public class CategoryList
    {
        public CategoryList()
        {
            _categories = new List<Category>();
        }

        public void Add(Category item)
        {
            _categories.Add(item);
        }

        public IEnumerable<Category> GetCategories()
        {
            foreach (var cat in _categories)
                yield return cat;
        }

        #region Fields
        List<Category> _categories;
        #endregion
    }

    public class Category : IComparable<Category>, IEquatable<Category>, ICloneable<Category>
    {
        #region Initialization
        private Category()
        {
        }

        public Category(string root, string name, string abbr)
        {
            this.Root = root;
            this.Name = name;
            this.Abbreviation = abbr.TrimEnd('/');
        }
        #endregion

        public Category Clone()
        {
            return new Category(this.Root, this.Name, this.Abbreviation);
        }

        public bool Equals(Category o)
        {
            return o == null ? false : (this.CompareTo(o) == 0);
        }

        public int CompareTo(Category o)
        {
            int x;
            
            x = this.Root.CompareTo(o.Root);
            if (x != 0)
                return x;

            x = this.Abbreviation.CompareTo(o.Abbreviation);
            if (x != 0)
                return x;

            return this.Name.CompareTo(o.Name);
        }

        public override string ToString()
        {
            return Category.Serialize(this);
        }

        #region Serialization
        public static string Serialize(Category category)
        {
            string result = CsvParser.WriteLine(category.Root, category.Name, category.Abbreviation);

            return result;
        }

        public static Category Deserialize(string line)
        {
            List<string> values = CsvParser.ReadLine(line);
            Category category = new Category();

            Logger.AssertValue(values.Count, 3, "Category field count");

            category.Root = values[0];
            category.Name = values[1];
            category.Abbreviation = values[2];

            return category;
        }
        #endregion

        #region Properties
        public string Root { get; private set; }
        public string Name { get; private set; }
        public string Abbreviation { get; private set; }
        #endregion
    }
}
