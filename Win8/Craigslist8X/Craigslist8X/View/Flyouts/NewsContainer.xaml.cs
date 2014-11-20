using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.View
{
    public sealed partial class NewsContainer : UserControl, INotifyPropertyChanged
    {
        public NewsContainer()
        {
            this.InitializeComponent();
            this.DataContext = this;
        }

        public void SetContext(List<NewsItem> items)
        {
            this.News = new System.Collections.ObjectModel.ObservableCollection<NewsItem>(items);
            this.ItemsFlip.ItemsSource = this.News;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs("Filter"));
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        public ObservableCollection<NewsItem> News
        {
            get
            {
                return this._news;
            }
            set
            {
                this._news = value;
                this.OnPropertyChanged("News");
            }
        }

        ObservableCollection<NewsItem> _news;
    }
}
