using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public static class Utility
    {
        public static string GetSearchCityLabel()
        {
            string location = string.Empty;

            if (!CityManager.Instance.SearchCitiesDefined)
            {
                location = "undefined";
            }
            else
            {
                location = CityManager.Instance.SearchCities.First().DisplayName;
            }

            // More?
            if (CityManager.Instance.SearchCities.Count > 1)
            {
                location += string.Format(", +{0}", CityManager.Instance.SearchCities.Count - 1);
            }

            return location;
        }

        public static string GetSearchCategoryLabel()
        {
            return CategoryManager.Instance.SearchCategory == null ? "undefined" : CategoryManager.Instance.SearchCategory.Name;
        }
    }
}
