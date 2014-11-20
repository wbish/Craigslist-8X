using System;

using WB.Craigslist8X.Common;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public class SavedSearchVM : BindableBase
    {
        public SavedSearchVM(SavedQuery sq)
        {
            this._sq = sq;
            this._sq.PropertyChanged += SavedQuery_PropertyChanged;
        }

        void SavedQuery_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Notifications")
            {
                this.OnPropertyChanged("Notifications");
            }
        }

        public bool Selected
        {
            get
            {
                return this._selected;
            }
            set
            {
                this.SetProperty(ref this._selected, value);
            }
        }

        public string Name
        {
            get
            {
                return this._sq.Name;
            }
        }

        public string Notifications
        {
            get
            {
                return this._sq.Notifications == 0 ? string.Empty : this._sq.Notifications.ToString();
            }
        }

        public SavedQuery SavedQuery
        {
            get
            {
                return this._sq;
            }
        }

        SavedQuery _sq;
        bool _selected;
    }
}