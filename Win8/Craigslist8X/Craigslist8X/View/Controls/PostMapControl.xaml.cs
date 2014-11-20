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

namespace WB.Craigslist8X.View
{
    public sealed partial class PostMapControl : UserControl
    {
        public PostMapControl()
        {
            this.InitializeComponent();
        }

        public string GetPostData()
        {
            if (this.ShowMap.IsOn)
            {
                return string.Format("&wantamap=on&{0}={1}&{2}={3}&{4}={5}&{6}={7}&{8}={9}",
                    Uri.EscapeDataString(StreetField.Tag.ToString()),
                    Uri.EscapeDataString(StreetField.Text),
                    Uri.EscapeDataString(CrossStreetField.Tag.ToString()),
                    Uri.EscapeDataString(CrossStreetField.Text),
                    Uri.EscapeDataString(CityField.Tag.ToString()),
                    Uri.EscapeDataString(CityField.Text),
                    Uri.EscapeDataString(RegionField.Tag.ToString()),
                    Uri.EscapeDataString(RegionField.Text),
                    Uri.EscapeDataString(PostalField.Tag.ToString()),
                    Uri.EscapeDataString(PostalField.Text)
                    );
            }
            else
            {
                return string.Empty;
            }
        }
        
        private void ShowMap_Toggled(object sender, RoutedEventArgs e)
        {
            if (this.ShowMap.IsOn)
                this.MapGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            else
                this.MapGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }
    }
}
