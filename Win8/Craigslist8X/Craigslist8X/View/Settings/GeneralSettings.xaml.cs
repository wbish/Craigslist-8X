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

using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;

namespace WB.Craigslist8X.View
{
    public sealed partial class GeneralSettings : UserControl
    {
        public GeneralSettings()
        {
            this.InitializeComponent();

            this._vm = new GeneralSettingsVM();
            this.DataContext = this._vm;
        }
        
        GeneralSettingsVM _vm;
    }
}
