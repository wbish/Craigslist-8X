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
    public sealed partial class NumericFilter : UserControl, INotifyPropertyChanged
    {
        public NumericFilter()
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

        public QueryFilterNumeric Filter
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

        QueryFilterNumeric _filter;

        private void FilterValue_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Menu:
                case Windows.System.VirtualKey.F4:
                case Windows.System.VirtualKey.Number0:
                case Windows.System.VirtualKey.Number1:
                case Windows.System.VirtualKey.Number2:
                case Windows.System.VirtualKey.Number3:
                case Windows.System.VirtualKey.Number4:
                case Windows.System.VirtualKey.Number5:
                case Windows.System.VirtualKey.Number6:
                case Windows.System.VirtualKey.Number7:
                case Windows.System.VirtualKey.Number8:
                case Windows.System.VirtualKey.Number9:
                case Windows.System.VirtualKey.NumberPad0:
                case Windows.System.VirtualKey.NumberPad1:
                case Windows.System.VirtualKey.NumberPad2:
                case Windows.System.VirtualKey.NumberPad3:
                case Windows.System.VirtualKey.NumberPad4:
                case Windows.System.VirtualKey.NumberPad5:
                case Windows.System.VirtualKey.NumberPad6:
                case Windows.System.VirtualKey.NumberPad7:
                case Windows.System.VirtualKey.NumberPad8:
                case Windows.System.VirtualKey.NumberPad9:
                    break;
                case Windows.System.VirtualKey.Enter:
                    DummyButton.Focus(Windows.UI.Xaml.FocusState.Pointer);
                    e.Handled = true;
                    break;
                default:
                    if (sender as TextBox != null && (e.Key == Windows.System.VirtualKey.Decimal || e.Key == (Windows.System.VirtualKey)190))
                    {
                        e.Handled = (sender as TextBox).Text.Contains(".");
                    }
                    else
                    {
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}
