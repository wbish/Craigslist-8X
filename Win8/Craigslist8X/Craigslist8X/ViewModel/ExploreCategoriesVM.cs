using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.CraigslistApi;

namespace WB.Craigslist8X.ViewModel
{
    public class ExploreCategoriesVM : BindableBase
    {
        public ExploreCategoriesVM(Category root)
        {
            this.Root = root;

            IEnumerable<Category> categories;

            if (root == null)
            {
                // Top level category
                // Do we need to hide personals?
                if (Settings.Instance.PersonalsUnlocked)
                    categories = CategoryManager.Instance.Categories.GroupBy(x => x.Root).Select(x => x.First());
                else
                    categories = CategoryManager.Instance.Categories.GroupBy(x => x.Root).Select(x => x.First()).Where(x => x.Root != "personals");
            }
            else
            {
                categories = CategoryManager.Instance.Categories.Where(x => x.Root == root.Root);
            }

            this.Categories = new ObservableCollection<CategoryVM>(
                    from x in categories select new CategoryVM(x, root == null ? CategoryVM.DisplayField.Root : CategoryVM.DisplayField.Name)
                );

            CityManager.Instance.PropertyChanged += Instance_PropertyChanged;
        }

        void Instance_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SearchCities")
            {
                this.OnPropertyChanged("Title");
            }
        }

        #region Properties
        public ObservableCollection<CategoryVM> Categories
        {
            get
            {
                return this._categories;
            }
            set
            {
                this.SetProperty(ref this._categories, value);
            }
        }

        public string Title
        {
            get
            {
                if (this.Root == null)
                {
                    return string.Format(BrowseTitleFormat, Utility.GetSearchCityLabel());
                }
                else
                {
                    return string.Format(BrowseTitleFormat, this.Root.Root);
                }
            }
        }

        public Category Root
        {
            get;
            set;
        }
        #endregion

        #region Fields
        ObservableCollection<CategoryVM> _categories;
        #endregion

        #region Constants
        const string BrowseTitleFormat = "Browse {0}";
        #endregion
    }

    public class CategoryVM : BindableBase
    {
        public enum DisplayField
        {
            Root,
            Name,
        }

        public CategoryVM(Category cat, DisplayField field)
        {
            this.Category = cat;
            this._field = field;
        }

        public string Display
        {
            get
            {
                switch (this._field)
                {
                    case DisplayField.Name:
                        return this.Category.Name;
                    case DisplayField.Root:
                        return this.Category.Root;
                    default:
                        return string.Empty;
                }
            }
        }

        public Category Category
        {
            get;
            private set;
        }

        public bool Selected
        {
            get
            {
                return this._selected;
            }
            set
            {
                this.SetProperty(ref this._selected, value);
            }
        }

        DisplayField _field;
        bool _selected;
    }
}
