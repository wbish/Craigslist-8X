using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Callisto.Controls;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class SearchSettings : UserControl
    {
        public SearchSettings()
        {
            this.InitializeComponent();

            this._vm = new SearchSettingsVM();
            this.DataContext = this._vm;

            if (CityManager.Instance.SearchCitiesDefined)
            {
                this.GetStarted.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        #region Cities
        private void AddCity_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame root = (Frame)Window.Current.Content;
            root.Navigate(typeof(ChooseCategoryPage));

            if (this.Parent is SettingsFlyout)
            {
                SettingsFlyout sf = this.Parent as SettingsFlyout;
                sf.IsOpen = false;
            }
        }

        private void RemoveCity_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CraigCity city = (sender as FrameworkElement).DataContext as CraigCity;
            CityManager.Instance.RemoveSearchCity(city);
        }

        private void MoveCityToTop_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CraigCity city = (sender as FrameworkElement).DataContext as CraigCity;
            CityManager.Instance.PromoteSearchCity(city);
        }
        #endregion

        #region Change Category
        private void ChangeCategory_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (CategorySelector.Visibility == Windows.UI.Xaml.Visibility.Visible)
            {
                CategorySelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                ChangeCategory.Content = "Change Category";
            }
            else
            {
                CategorySelector.Visibility = Windows.UI.Xaml.Visibility.Visible;
                ChangeCategory.Content = "Cancel";

                this._category = CategoryHierarchy.Root;

                if (Settings.Instance.PersonalsUnlocked)
                    CategorySelector.ItemsSource = (from x in CategoryManager.Instance.Categories select x.Root).Distinct().OrderBy(x => x);
                else
                    CategorySelector.ItemsSource = (from x in CategoryManager.Instance.Categories select x.Root).Distinct().Where(x => !x.Equals("personals", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x);
            }
        }

        private void Category_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string context = (sender as FrameworkElement).DataContext as string;
            Logger.Assert(!string.IsNullOrEmpty(context), "Category picker context appears to be empty");
            if (string.IsNullOrEmpty(context))
                return;

            switch (this._category)
            {
                case CategoryHierarchy.Root:
                    {
                        CategorySelector.ItemsSource = (from x in CategoryManager.Instance.Categories.Where(x => x.Root == context) select x.Name).Distinct().OrderBy(x => x);
                        this._category = CategoryHierarchy.Category;
                        break;
                    }
                case CategoryHierarchy.Category:
                    {
                        var cat = (from x in CategoryManager.Instance.Categories.Where(x => x.Name == context) select x).FirstOrDefault();
                        this.SetCategory(cat);
                        break;
                    }
                default:
                    throw new ArgumentException(this._category.ToString());
            }
        }

        private void SetCategory(Category cat)
        {
            CategorySelector.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            ChangeCategory.Content = "Change Category";

            CategoryManager.Instance.SearchCategory = cat;
            this._vm.OnPropertyChanged("SearchCategory");
        }

        CategoryHierarchy _category;
        enum CategoryHierarchy
        {
            Root,
            Category,
        }
        #endregion

        SearchSettingsVM _vm;
    }
}