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

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class ChooseCategoryPage : LayoutAwarePage
    {
        public ChooseCategoryPage()
        {
            this.InitializeComponent();

            this._vm = new ChooseCitiesVM();
            this.DataContext = this._vm;

            this.SetContinentSelections();
        }

        private async void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame frame = await App.EnsureNavigationFrame();
            frame.Content = MainPage.Instance;

            SettingsUI.ShowSearchSettings();
        }

        private void TitleButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ContinentPicker cp = new ContinentPicker();
            Flyout flyout = new Flyout();
            flyout.Content = cp;
            flyout.Placement = PlacementMode.Bottom;
            flyout.PlacementTarget = this.titleButton;
            flyout.IsOpen = true;
            flyout.Closed += (a, b) => 
                {
                    if (!string.IsNullOrEmpty(cp.Continent) && this._vm.Continent != cp.Continent)
                    {
                        this._blockRemove = true;
                        this.CitiesGrid.ScrollIntoView(this._vm.States.First().Cities.First());
                        this._vm.Continent = cp.Continent;
                        this.SetContinentSelections();
                        this._blockRemove = false;
                    }
                };
        }

        private void CitiesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null)
            {
                foreach (var c in e.AddedItems)
                    CityManager.Instance.AddSearchCity(c as CraigCity);
            }

            if (e.RemovedItems != null && !this._blockRemove)
            {
                foreach (var c in e.RemovedItems)
                    CityManager.Instance.RemoveSearchCity(c as CraigCity);
            }
        }

        private void SetContinentSelections()
        {
            foreach (var c in CityManager.Instance.SearchCities.Where(x => x.Continent == this._vm.Continent))
            {
                this.CitiesGrid.SelectedItems.Add(c);
            }
        }

        ChooseCitiesVM _vm;
        bool _blockRemove;
    }
}
