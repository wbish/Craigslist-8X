using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Security.Credentials;

using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.View;
using WB.Craigslist8X.ViewModel;
using WB.SDK.Logging;

namespace WB.Craigslist8X.View
{
    public sealed partial class ChooseAccountPanel : UserControl, IPanel
    {
        public ChooseAccountPanel()
        {
            this.InitializeComponent();
            this._msgLock = new AsyncLock();
        }

        public Task AttachContext(object context, IPanel parent)
        {
            UserAccounts.Instance.Accounts.CollectionChanged += Accounts_CollectionChanged;
            this._purpose = (ChooseAccountPurpose)context;

            this.PopulateList();

            return null;
        }

        void Accounts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.PopulateList();
        }

        private void PopulateList()
        {
            List<AccountVMBase> accountList = (from x in UserAccounts.Instance.Accounts select new AccountVM(x)).ToList<AccountVMBase>();

            if (this._purpose == ChooseAccountPurpose.CreatePost)
            {
                accountList.Add(new AnonymousVM());
            }

            accountList.Add(new AddAccountVM());

            this.AccountsItemList.ItemsSource = accountList;
        }

        private void ListView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            ScrollViewer scroller = Utilities.GetVisualChild<ScrollViewer>(this.AccountsItemList);
            int delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (delta > 0)
            {
                if (scroller.VerticalOffset <= 2.01)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
            else if (delta < 0)
            {
                if (scroller.VerticalOffset >= scroller.ExtentHeight - scroller.ViewportHeight)
                {
                    MainPage.Instance.PanelView.PanelView_PointerWheelChanged(sender, e);
                }
            }
        }

        private async void ChooseAccount_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement container = sender as FrameworkElement;

            using (await this._msgLock.LockAsync())
            {
                if (container != null && container.DataContext as AccountVMBase != null)
                {
                    AccountVMBase accountVM = (AccountVMBase)container.DataContext;

                    if (accountVM.Type == AccountVMBase.ViewModelType.Add)
                    {
                        await AddAccountAsync();
                        return;
                    }
                    else
                    {
                        // There are two other accountVM.Types: Anonymous and logged in user.
                        // If we are in account management, the anonymous option should not be visible.
                        switch (this._purpose)
                        {
                            case ChooseAccountPurpose.CreatePost:
                                await this.CreatePostAsync(container.DataContext as AccountVM);
                                break;
                            case ChooseAccountPurpose.AccountManagement:
                                await this.ManageAccountAsync(container.DataContext as AccountVM);
                                break;
                            default:
                                await Logger.AssertNotReached("Unknown purpose type.");
                                break;
                        }
                    }
                }
            }
        }

        private async void DeleteAccount_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            if (!(sender is FrameworkElement) || !((sender as FrameworkElement).DataContext is AccountVM))
            {
                await Logger.AssertNotReached("We expect an account item");
                return;
            }

            AccountVM account = ((sender as FrameworkElement).DataContext as AccountVM);
            UserAccounts.Instance.Remove(account.Account);
        }

        private async Task AddAccountAsync()
        {
            PasswordCredential account = await UserAccounts.PromptForCreds("Add your Craigslist username and password to Craigslist 8X");

            if (account != null)
            {
                account.RetrievePassword();

                if (!await Craigslist.Instance.ValidateCreds(account.UserName, account.Password))
                {
                    await new MessageDialog("We have failed to login to craigslist using the credentials provided.", "Craigslist 8X").ShowAsync();
                }
                else
                {
                    UserAccounts.Instance.Add(account);
                }
            }
        }

        private async Task CreatePostAsync(AccountVM avm)
        {
            await Logger.Assert(CityManager.Instance.SearchCitiesDefined, "How did we get this far without defining search cities?");
            if (!CityManager.Instance.SearchCitiesDefined)
            {
                await new MessageDialog("Please define your search cities before proceeding.", "Craigslist 8X").ShowAsync();
                SettingsUI.ShowSearchSettings();
                return;
            }

            if (!App.IsPro && Settings.Instance.InAppPostCount >= InAppFreeLimit)
            {
                MessageDialog msg = new MessageDialog(@"Sorry, you have reached the in app post limit. Please upgrade to Craigslist 8X PRO to unlock unlimited in app posts and other goodies.");
                await msg.ShowAsync();
                return;
            }

            PasswordCredential acct = avm != null ? avm.Account : null;

            // There is actually more than one city, let the user pick which one he wants to post to.
            List<CraigCity> cities = (from x in CityManager.Instance.SearchCities select CityManager.Instance.GetCityByUri(x.Location)).Distinct().ToList();
            if (cities.Count > 1)
            {
                await MainPage.Instance.ExecuteChooseCity(this, acct);
            }
            else
            {
                CreatePostContext context = new CreatePostContext();
                context.Account = acct;
                context.City = cities.First();

                await MainPage.Instance.ExecuteCreatePost(this, context);
            }
        }

        private async Task ManageAccountAsync(AccountVM avm)
        {
            if (!App.IsPro)
            {
                MessageDialog msg = new MessageDialog(@"Sorry, this is a Craigslist 8X Pro only feature. Please upgrade to Craigslist 8X PRO to unlock account management, unlimited in app posts and other goodies.");
                await msg.ShowAsync();
                return;
            }

            PasswordCredential acct = avm != null ? avm.Account : null;

            if (acct == null)
            {
                await Logger.AssertNotReached("Should have valid user credentials");
                return;
            }

            await MainPage.Instance.ExecuteAccountManagement(this, acct);
        }

        private ChooseAccountPurpose _purpose;
        private AsyncLock _msgLock;

        const int InAppFreeLimit = 3;
    }

    public enum ChooseAccountPurpose
    {
        CreatePost,
        AccountManagement,
    }
}
