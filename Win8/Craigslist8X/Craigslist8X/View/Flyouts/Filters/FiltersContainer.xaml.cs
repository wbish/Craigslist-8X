using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using WB.CraigslistApi;

namespace WB.Craigslist8X.View
{
    public sealed partial class FiltersContainer : UserControl
    {
        public FiltersContainer()
        {
            this.InitializeComponent();
        }

        public void AttachContext(Query query)
        {
            if (query == null)
                throw new ArgumentNullException("query");

            Query q = query.Clone();

            if (q.Filters == null)
                this.Filters = WB.CraigslistApi.QueryFilter.GetFilters(q.Category);
            else
                this.Filters = q.Filters;

            foreach (var filter in this.Filters)
            {
                if (filter is QueryFilterBoolean)
                {
                    this.FilterPanel.Children.Add(new BooleanFilter() { Filter = filter as QueryFilterBoolean });
                }
                else if (filter is QueryFilterNumeric)
                {
                    this.FilterPanel.Children.Add(new NumericFilter() { Filter = filter as QueryFilterNumeric });
                }
                else if (filter is QueryFilterChooseOne)
                {
                    this.FilterPanel.Children.Add(new ChooseOneFilter() { Filter = filter as QueryFilterChooseOne });
                }
            }

            this.SearchQuery = q.Text;
            this.TitlesOnly = q.Type == Query.QueryType.TitleOnly;
            this.HasPictures = q.HasImage;

            this.DataContext = this;
        }

        public string SearchQuery
        {
            get;
            set;
        }

        public bool TitlesOnly
        {
            get;
            set;
        }

        public bool HasPictures
        {
            get;
            set;
        }

        public bool HasFilters
        {
            get
            {
                return Filters != null && Filters.Count > 0;
            }
        }

        public QueryFilters Filters
        {
            get;
            set;
        }

        public Query Original
        {
            get;
            set;
        }

        private void FilterContainer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            DummyButton.Focus(Windows.UI.Xaml.FocusState.Pointer);
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                DummyButton.Focus(Windows.UI.Xaml.FocusState.Pointer);
            }
        }
    }
}
