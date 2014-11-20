using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public sealed partial class ChooseOneFilter : UserControl, INotifyPropertyChanged
    {
        public ChooseOneFilter()
        {
            this.InitializeComponent();

            this.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs("Filter"));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        public QueryFilterChooseOne Filter
        {
            get
            {
                return this._filter;
            }
            set
            {
                this._filter = value;
                this.OnPropertyChanged("Filter");
            }
        }

        QueryFilterChooseOne _filter;
    }
}