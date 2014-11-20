using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Streams;

using WinRTXamlToolkit.Async;

using WB.CraigslistApi;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.Model
{
    internal class CategoryManager : BindableBase, IStorageBacked
    {
        #region Constructors
        static CategoryManager()
        {
            _instanceLock = new object();
        }

        private CategoryManager()
        {
            this._fileLock = new AsyncLock();
        }
        #endregion

        #region Singleton
        public static CategoryManager Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                        _instance = new CategoryManager();

                    return _instance;
                }
            }
        }

        static CategoryManager _instance;
        static object _instanceLock;
        #endregion

        #region IStorageBacked
        public async Task<bool> LoadAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.LocalFolder.GetFileAsync(CraigslistCategoriesFileName);
            }
            catch (System.IO.FileNotFoundException)
            {
            }

            if (file == null)
            {
                file = await SDK.Utilities.GetPackagedFile("Resources", CraigslistCategoriesFileName);
            }

            if (file != null)
            {
                Logger.LogMessage("Categories", "Reading CraigslistCategories.csv");
                
                try
                {
                    string content = null;

                    using (await this._fileLock.LockAsync())
                    {
                        content = await FileIO.ReadTextAsync(file);
                    }

                    CategoryList data = new CategoryList();

                    foreach (var line in content.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        Category city = Category.Deserialize(line.Trim());
                        data.Add(city);
                    }

                    await Logger.Assert(data.GetCategories().Count() > 0, "No categories found in file!");

                    if (this.Categories == null)
                        this.Categories = new ObservableCollection<Category>(data.GetCategories());
                    else
                        this.Categories.Copy(data.GetCategories());

                    _cachedCategories = data;

                    if (SearchCategory == null)
                    {
                        // Set the default category
                        SearchCategory = (from x in _categories where x.Name.Equals(DefaultCategoryName, StringComparison.OrdinalIgnoreCase) select x).First();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogMessage("Categories", "Failed to read CraigslistCategories.csv");
                    Logger.LogException(ex);
                }
            }

            return false;
        }

        public async Task<bool> SaveAsync()
        {
            if (_cachedCategories == null)
            {
                await Logger.AssertNotReached("No cached categories.");
                return false;
            }

            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (var city in _cachedCategories.GetCategories())
                {
                    sb.AppendLine(Category.Serialize(city));
                }

                using (await this._fileLock.LockAsync())
                {
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(CraigslistCategoriesFileName, CreationCollisionOption.ReplaceExisting);
                    await file.SaveFileAsync(sb.ToString());
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debugger.Break();
                Logger.LogMessage("CraigslistCategories", "Failed to save CraigslistCategories.csv");
                Logger.LogException(ex);
            }

            return false;
        }
        
        private AsyncLock _fileLock;
        #endregion

        #region Public Properties
        public bool CategoriesLoaded
        {
            get
            {
                return _cachedCategories != null;
            }
        }

        public Category SearchCategory
        {
            get
            {
                return Category.Deserialize(Settings.Instance.SearchCategory);
            }
            set
            {
                Settings.Instance.SearchCategory = Category.Serialize(value);
                this.OnPropertyChanged("SearchCategory");
            }
        }

        public ObservableCollection<Category> Categories
        {
            get
            {
                return _categories;
            }
            private set
            {
                this.SetProperty(ref this._categories, value);
            }
        }
        #endregion

        #region Fields
        ObservableCollection<Category> _categories;
        CategoryList _cachedCategories;
        #endregion

        #region Constants
        internal const string DefaultCategoryName = "all for sale";
        internal const string CraigslistCategoriesFileName = "CraigslistCategories.csv";
        #endregion
    }
}