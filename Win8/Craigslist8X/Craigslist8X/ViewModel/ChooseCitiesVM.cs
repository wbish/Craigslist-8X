using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WB.Craigslist8X.Model;
using WB.Craigslist8X.Common;
using WB.CraigslistApi;

namespace WB.Craigslist8X.ViewModel
{
    public class ChooseCitiesVM : BindableBase
    {
        public ChooseCitiesVM()
        {
            if (CityManager.Instance.SearchCitiesDefined)
            {
                this.Continent = CityManager.Instance.SearchCities.First().Continent;
            }
            else
            {
                this.Continent = "US";
            }
        }

        public string Continent
        {
            get
            {
                return this._continent;
            }
            set
            {
                if (this.SetProperty(ref this._continent, value))
                {
                    if (this.States == null)
                        this.States = new ObservableCollection<CitiesByState>();
                    else
                        this.States.Clear();

                    if (CityManager.Instance.Cities != null)
                    {
                        foreach (var s in CityManager.Instance.Cities.Where(x => x.Continent == this.Continent).GroupBy(x => x.State).Select(x => x.First()))
                        {
                            CitiesByState state = new CitiesByState(s.State);
                            this.States.Add(state);

                            foreach (var c in CityManager.Instance.Cities.Where(x => x.State == s.State))
                            {
                                state.Cities.Add(c);
                            }
                        }
                    }
                }
            }
        }

        public ObservableCollection<CitiesByState> States
        {
            get
            {
                return this._states;
            }
            set
            {
                this.SetProperty(ref this._states, value);
            }
        }

        ObservableCollection<CitiesByState> _states;
        string _continent;
    }

    public class CitiesByState
    {
        public CitiesByState(string state)
        {
            this.State = state;
            this.Cities = new ObservableCollection<CraigCity>();
        }

        public string State
        {
            get;
            private set;
        }

        public ObservableCollection<CraigCity> Cities
        {
            get;
            private set;
        }
    }
}
