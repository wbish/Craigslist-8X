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

using Callisto.Controls;

namespace WB.Craigslist8X.View
{
    public sealed partial class ContinentPicker : UserControl
    {
        public ContinentPicker()
        {
            this.InitializeComponent();
        }

        private void TextBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TextBlock txt = sender as TextBlock;
            this.Continent = txt.Text;

            if (this.Parent is Flyout)
            {
                Flyout f = this.Parent as Flyout;
                f.IsOpen = false;
            }
        }

        public string Continent
        {
            get;
            set;
        }
    }
}
