using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;

using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class UpgradePanel : UserControl, IPanel
    {
        public UpgradePanel()
        {
            this.InitializeComponent();
        }

        #region IPanel
        public async Task AttachContext(object context, IPanel parent)
        {
            await Logger.Assert(!App.IsPro, "PRO package has already been purchased!");
        }
        #endregion

        private async void UpgradePro_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (App.IsPro)
            {
                await new MessageDialog("You have already purchased the PRO package.", "Craigslist 8X").ShowAsync();
                return;
            }

            bool success = false;
            try
            {
#if DEBUG
                await CurrentAppSimulator.RequestProductPurchaseAsync(App.Craigslist8XPRO, false);
#else
                await CurrentApp.RequestProductPurchaseAsync(App.Craigslist8XPRO, false);
#endif

                success = true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            if (!success)
            {
                await new MessageDialog("There was a problem trying to complete your purchase. Please try again.", "Craigslist 8X").ShowAsync();
                return;
            }

            if (!App.IsPro)
            {
                await new MessageDialog("The Craigslist 8X PRO package purchase was not completed.", "Craigslist 8X").ShowAsync();
                return;
            }
            else
            {
                MainPage.Instance.MainMenu.SetPurchasedPro();
                await new MessageDialog("Thank you for supporting Craigslist 8X!", "Craigslist 8X").ShowAsync();
            }
        }
    }
}
