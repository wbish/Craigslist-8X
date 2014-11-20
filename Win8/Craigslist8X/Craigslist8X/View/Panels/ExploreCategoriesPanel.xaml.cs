using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class ExploreCategoriesPanel : UserControl, IPanel
    {
        public ExploreCategoriesPanel()
        {
            this.InitializeComponent();

            this.DataContext = this;
        }

        public Task AttachContext(object context, IPanel parent)
        {
            Category cat = context as Category;
            this.RefreshRootCategory(cat);

            return null;
        }

        private void SearchItemsList_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ScrollViewer scroller = Utilities.GetVisualChild<ScrollViewer>(SearchItemsList);
            int delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (delta > 0)
            {
                if (scroller.VerticalOffset <= 2.01)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
            else if (delta < 0)
            {
                if (scroller.VerticalOffset >= scroller.ExtentHeight - scroller.ViewportHeight)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
        }

        private async void Category_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Border el = sender as Border;

            if (el == null)
                return;

            CategoryVM cvm = (el.DataContext as CategoryVM);
            Category cat = cvm.Category;

            this.SetSelection(cvm);

            if (this._vm.Root == null)
            {
                List<Category> categories = CategoryManager.Instance.Categories.Where(x => x.Root == cat.Root).ToList();
                if (categories.Count() == 1)
                {
                    Query template = new Query(CityManager.Instance.SearchCities.First(), categories.First(), string.Empty);
                    template.HasImage = Settings.Instance.OnlyShowPostsPictures;
                    QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);
                    
                    await MainPage.Instance.ExecuteSearchQuery(this, qb);
                }
                else
                {
                    await MainPage.Instance.ExecuteBrowse(this, cat);
                }
            }
            else
            {
                Query template = new Query(CityManager.Instance.SearchCities.First(), cat, string.Empty);
                template.HasImage = Settings.Instance.OnlyShowPostsPictures;
                QueryBatch qb = QueryBatch.BuildQueryBatch(CityManager.Instance.SearchCities, template);

                await MainPage.Instance.ExecuteSearchQuery(this, qb);
            }
        }

        private void SetSelection(CategoryVM item)
        {
            if (this._selected != null)
            {
                this._selected.Selected = false;
            }

            this._selected = item;
            item.Selected = true;
        }

        private void RefreshRootCategory(Category root)
        {
            this._vm = new ExploreCategoriesVM(root);
            this.DataContext = this._vm;
        }

        ExploreCategoriesVM _vm;
        CategoryVM _selected;
    }
}
