using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;

using WB.CraigslistApi;
using WB.Craigslist8X.Model;
using WB.Craigslist8X.Common;
using WB.SDK.Logging;

namespace WB.Craigslist8X.ViewModel
{
    public sealed class SearchResultsVM : BindableBase
    {
        public SearchResultsVM(QueryBatch qb)
        {
            this._qb = qb;
            this.SearchCities = new ObservableCollection<CraigCity>();

            foreach (var q in qb.Queries)
            {
                this.SearchCities.Add(q.City);
            }
        }

        #region Methods
        public event EventHandler QueryExecuted;

        public async Task ExecuteQueryAsync(CancellationToken token)
        {
            this.LoadingItems = true;

            List<QueryResult> qrs = null;

            try
            {
                qrs = await this._qb.Execute(token);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogException(ex);
            }

            if (qrs != null)
            {
                this.QuerySucceeded = true;
                this.QueryResults = (from x in qrs select new QueryResultVM(this, x)).ToList();

                foreach (var qr in this.QueryResults)
                {
                    qr.PostItems.CollectionChanged += PostItems_CollectionChanged;
                }

                this._postGroups = new ObservableCollection<PostGroup>();

                for (int i = 0; i < this.QueryResults.Count; ++i)
                {
                    QueryResultVM qr = this.QueryResults[i];
                    PostGroup group = new PostGroup() { City = qr.QueryResult.Query.City };
                    this._postGroups.Add(group);

                    foreach (var p in qr.PostItems)
                    {
                        group.Add(p);
                    }
                }
            }

            if (this.SelectedCity == null)
            {
                this.SelectedCity = this._qb.Queries.First().City;
            }
            else
            {
                // HACK
                CraigCity city = this.SelectedCity;
                this._city = null;
                this.SelectedCity = city;
            }

            this.LoadingItems = false;

            if (this.QueryExecuted != null)
                this.QueryExecuted(this, null);
        }

        public QueryResultVM GetCityQueryResult(CraigCity city)
        {
            if (this.QueryResults != null)
            {
                foreach (var qr in this.QueryResults)
                {
                    if (qr.QueryResult.Query.City.Equals(city))
                    {
                        return qr;
                    }
                }
            }

            return null;
        }

        async void PostItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var qr in this.QueryResults)
            {
                // Was the right sub list modified?
                if (qr.PostItems == sender)
                {
                    // Find the right group for the grid view
                    PostGroup group = this._postGroups.Where(x => x.City.Equals(qr.QueryResult.Query.City)).First();
                    await Logger.AssertNotNull(group, "we couldn't find the post group??");

                    // Add or remove the right posts
                    if (e.Action == NotifyCollectionChangedAction.Remove)
                    {
                        group.Remove(group.Last());
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        for (int j = 0; j < e.NewItems.Count; ++j)
                        {
                            PostBase post = e.NewItems[j] as PostBase;
                            group.Add(e.NewItems[j] as PostBase);
                        }
                    }

                    break;
                }
            }
        }
        #endregion

        #region Properties
        public bool ShowAds
        {
            get
            {
                return !App.IsPro;
            }
        }

        public bool FiltersSet
        {
            get
            {
                if (this.FirstQuery.HasImage
                    || this.FirstQuery.Type == Query.QueryType.TitleOnly
                    || (this.FirstQuery.Filters != null && this.FirstQuery.Filters.FiltersSet())
                    )
                    return true;

                return false;
            }
        }

        public bool SortSet
        {
            get
            {
                return this.FirstQuery.Sort != Query.SortOrder.Recent;
            }
        }

        public bool QuerySucceeded
        {
            get;
            set;
        }

        public string SearchTitle
        {
            get
            {
                if (string.IsNullOrEmpty(this.FirstQuery.Text))
                    return string.Format(BrowsingPosts, this.FirstQuery.Category.Name);
                else
                    return string.Format(SearchingPosts, this.FirstQuery.Text, this.FirstQuery.Category.Name);
            }
        }

        public ObservableCollection<PostBase> PostItems
        {
            get
            {
                return this._postItems;
            }
            private set
            {
                this.SetProperty(ref this._postItems, value);
            }
        }

        public List<QueryResultVM> QueryResults
        {
            get
            {
                return _qrVM;
            }
            private set
            {
                this.SetProperty(ref this._qrVM, value);
            }
        }

        public ObservableCollection<CraigCity> SearchCities
        {
            get
            {
                return this._searchCities;
            }
            set
            {
                this.SetProperty(ref this._searchCities, value);
            }
        }

        public CraigCity SelectedCity
        {
            get
            {
                return this._city;
            }
            set
            {
                if (value == null)
                    return;

                if (this._city == null || !this._city.Equals(value))
                {
                    this.SetProperty(ref this._city, value);

                    if (this._postGroups == null)
                        return;

                    foreach (var c in this._postGroups)
                    {
                        if (this._city.Equals(c.City))
                        {
                            this.PostItems = c;
                            break;
                        }
                    }
                }
            }
        }

        public bool LoadingItems
        {
            get
            {
                return _loadingItems;
            }
            set
            {
                this.SetProperty(ref this._loadingItems, value);
            }
        }

        public Query FirstQuery
        {
            get
            {
                return this._qb.Queries.First();
            }
        }

        public bool CollapseOnSelection
        {
            get
            {
                return Settings.Instance.CollapseOnSelection;
            }
            set
            {
                Settings.Instance.CollapseOnSelection = value;
            }
        }
        #endregion

        #region Fields
        CraigCity _city;
        QueryBatch _qb;
        List<QueryResultVM> _qrVM;
        ObservableCollection<PostBase> _postItems;
        ObservableCollection<PostGroup> _postGroups;
        ObservableCollection<CraigCity> _searchCities;
        bool _loadingItems;
        #endregion

        #region Constants
        const string BrowsingPosts = "Browsing {0}";
        const string SearchingPosts = "{0} in {1}";
        #endregion
    }

    public class PostGroup : ObservableCollection<PostBase>
    {
        public CraigCity City
        {
            get;
            set;
        }
    }
}