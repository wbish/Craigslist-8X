using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Security;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;

using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.Model
{
    public class UserAccounts : BindableBase, IStorageBacked
    {
        #region Initialization
        static UserAccounts()
        {
            _instanceLock = new object();
        }

        private UserAccounts()
        {
            this.Accounts = new ObservableCollection<PasswordCredential>();
            this._listLock = new object();
            this._vault = new PasswordVault();

            this.Accounts.CollectionChanged += Accounts_CollectionChanged;
        }

        void Accounts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.OnPropertyChanged("Accounts");
        }
        #endregion

        #region Singleton
        public static UserAccounts Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new UserAccounts();

                    return _instance;
                }
            }
        }

        static UserAccounts _instance;
        static object _instanceLock;
        #endregion

        public async static Task<PasswordCredential> PromptForCreds(string message)
        {
            CredentialPickerOptions options = new CredentialPickerOptions();
            options.Message = message;
            options.Caption = "Craigslist 8X";
            options.TargetName = "craigslist.org";
            options.CallerSavesCredential = false;
            options.CredentialSaveOption = CredentialSaveOption.Hidden;
            options.AuthenticationProtocol = AuthenticationProtocol.Basic;

            var result = await CredentialPicker.PickAsync(options);

            if (!string.IsNullOrWhiteSpace(result.CredentialUserName) && !string.IsNullOrWhiteSpace(result.CredentialPassword))
                return new PasswordCredential(CraigslistResource, result.CredentialUserName, result.CredentialPassword);
            else
                return null;
        }

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            try
            {
                // We used to save usernames and passwords in a very unsafe way. Lets delete the old file.
                StorageFile file = await ApplicationData.Current.RoamingFolder.GetFileAsync(UserAccountsFileName);
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception)
            {
                // File no longer exists. Maybe we can get rid of this try/catch block some day
            }

            if (this._vault == null)
            {
                await Logger.AssertNotReached("Why is vault null?");
                return false;
            }

            lock (this._listLock)
            {
                this.Accounts.Clear();

                try
                {
                    foreach (var cred in this._vault.FindAllByResource(CraigslistResource))
                    {
                        this.Accounts.Add(cred);
                    }
                }
                catch (Exception)
                {
                    // Resource not found
                }
            }

            return true;
        }

        #pragma warning disable 1998
        public async Task<bool> SaveAsync()
        {
            return false;
        }
        #pragma warning restore 1998
        #endregion

        #region Methods
        public void Add(PasswordCredential cred)
        {
            // Ensure the right resource is set
            cred.Resource = CraigslistResource;

            this.Remove(cred);

            lock (_listLock)
            {
                this._accounts.Add(cred);
                this._vault.Add(cred);
            }

            this.OnPropertyChanged("Accounts");
        }

        public void Remove(PasswordCredential cred)
        {
            lock (_listLock)
            {
                for (int i = 0; i < this._accounts.Count; ++i)
                {
                    if (cred.UserName.Equals(this._accounts[i].UserName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        this._accounts.RemoveAt(i);
                        --i;

                        this._vault.Remove(cred);
                    }
                }
            }

            this.OnPropertyChanged("Accounts");
        }
        #endregion

        #region Properties
        public ObservableCollection<PasswordCredential> Accounts
        {
            get
            {
                return _accounts;
            }
            private set
            {
                this.SetProperty(ref this._accounts, value);
            }
        }
        #endregion

        #region Fields
        ObservableCollection<PasswordCredential> _accounts;
        object _listLock;
        PasswordVault _vault;
        #endregion

        #region Constants
        const string CraigslistResource = "http://www.craigslist.org";

        //file = await ApplicationData.Current.RoamingFolder.GetFileAsync(UserAccountsFileName);
        const string UserAccountsFileName = "UserAccounts.xml";
        #endregion
    }
}