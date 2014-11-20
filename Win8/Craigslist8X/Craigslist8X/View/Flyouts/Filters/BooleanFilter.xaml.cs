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
    public sealed partial class BooleanFilter : UserControl, INotifyPropertyChanged
    {
        public BooleanFilter()
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

        public QueryFilterBoolean Filter
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

        QueryFilterBoolean _filter;
    }
}
