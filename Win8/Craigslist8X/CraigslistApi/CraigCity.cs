using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;

using WB.SDK;
using WB.SDK.Logging;
using WB.SDK.Parsing;

namespace WB.CraigslistApi
{
    public class CraigCity : IComparable<CraigCity>, IEquatable<CraigCity>, ICloneable<CraigCity>
    {
        #region Constructor
        private CraigCity()
        {
        }

        public CraigCity(Uri location, string continent, string state, string city)
        {
            this.Location = location;
            this.Continent = continent;
            this.State = state;
            this.City = city;
            this.SubArea = string.Empty;
            this.SubAreaName = string.Empty;
        }

        public CraigCity(Uri location, string continent, string state, string city, Uri subUrl, string sub, string subName)
            : this(location, continent, state, city)
        {
            this.SubArea = sub;
            this.SubAreaName = subName;
            this.SubLocation = subUrl;
        }
        #endregion

        #region Overrides
        public CraigCity Clone()
        {
            return CraigCity.Deserialize(CraigCity.Serialize(this));
        }

        public bool Equals(CraigCity o)
        {
            return (o == null) ? false : this.CompareTo(o) == 0;
        }

        public int CompareTo(CraigCity o)
        {
            int result;

            result = this.Continent.CompareTo(o.Continent);
            if (result != 0)
                return result;

            result = this.State.CompareTo(o.State);
            if (result != 0)
                return result;

            result = this.City.CompareTo(o.City);
            if (result != 0)
                return result;

            result = this.IsSubArea.CompareTo(o.IsSubArea);
            if (result != 0)
                return result;

            if (!this.IsSubArea)
            {
                Logger.Assert(!o.IsSubArea, "isSubArea");
                return 0;
            }

            return this.SubArea.CompareTo(o.SubArea);
        }

        public override string ToString()
        {
            return CraigCity.Serialize(this);
        }
        #endregion

        #region Serialization
        public static CraigCity Deserialize(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;

            List<string> fields = CsvParser.ReadLine(line);
            CraigCity city = new CraigCity();

            city.Location = new Uri(fields[0]);
            city.Continent = fields[1];
            city.State = fields[2];
            city.City = fields[3];
            city.SubArea = fields[4];
            city.SubAreaName = fields[5];
            city.SubLocation = string.IsNullOrEmpty(city.SubArea) ? null : new Uri(string.Format("{0}{1}", city.Location, city.SubArea));

            if (fields.Count == 8)
            {
                city.Latitude = string.IsNullOrWhiteSpace(fields[6]) ? double.MinValue : double.Parse(fields[6]);
                city.Longitude = string.IsNullOrWhiteSpace(fields[7]) ? double.MinValue : double.Parse(fields[7]);
            }
            else
            {
                city.Latitude = double.MinValue;
                city.Longitude = double.MinValue;
            }

            return city;
        }

        public static string Serialize(CraigCity city)
        {
            string lat = city.Latitude == double.MinValue ? string.Empty : city.Latitude.ToString();
            string lon = city.Longitude == double.MinValue ? string.Empty : city.Longitude.ToString();
            string result = CsvParser.WriteLine(city.Location.AbsoluteUri, city.Continent, city.State, city.City, city.SubArea, city.SubAreaName, lat, lon);

            return result;
        }
        #endregion

        #region Properties
        public bool IsSubArea
        {
            get
            {
                return !string.IsNullOrEmpty(SubArea);
            }
        }
        public string DisplayName
        {
            get
            {
                if (this.IsSubArea)
                    return this.SubAreaName;
                else
                    return this.City;
            }
        }
        public Uri Location { get; set; }
        public string Continent { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public Uri SubLocation { get; set; }
        public string SubArea { get; set; }
        public string SubAreaName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        #endregion
    }

    public class CraigCityList
    {
        #region Constructor
        public CraigCityList()
        {
            _cities = new List<CraigCity>();
        }
        #endregion

        #region Methods
        public async Task Save(StorageFile file)
        {
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var outStream = stream.GetOutputStreamAt(0);
            var writer = new DataWriter(outStream);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var city in _cities)
                sb.AppendLine(city.ToString());

            writer.WriteString(sb.ToString());
            await outStream.FlushAsync();
        }

        public void Add(CraigCity city)
        {
            _cities.Add(city);
        }

        public bool Contains(CraigCity city)
        {
            return _cities.Contains(city);
        }

        public IEnumerable<CraigCity> GetCities()
        {
            foreach (var city in _cities)
                yield return city;
        }

        public IEnumerable<string> GetContinents()
        {
            return (from city in _cities select city.Continent).Distinct();
        }

        public IEnumerable<CraigCity> GetCitiesByContinent(string continent)
        {
            foreach (CraigCity city in _cities)
            {
                if (city.Continent.Equals(continent, StringComparison.OrdinalIgnoreCase))
                    yield return city;
            }
        }

        public IEnumerable<CraigCity> GetCitiesByState(string state)
        {
            foreach (CraigCity city in _cities)
            {
                if (city.State.Equals(state, StringComparison.OrdinalIgnoreCase))
                    yield return city;
            }
        }

        public CraigCity GetCityByName(string name)
        {
            foreach (var city in _cities)
            {
                if (!string.IsNullOrEmpty(city.SubAreaName) && city.SubAreaName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return city;
                else if (string.IsNullOrEmpty(city.SubAreaName) && city.City.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return city;
            }

            return null;
        }

        public CraigCity GetCityByUri(Uri location)
        {
            foreach (var city in _cities)
            {
                if (city.Location.Equals(location))
                    return city;
            }

            return null;
        }
        #endregion

        #region Properties
        public int Count
        {
            get
            {
                return _cities.Count;
            }
        }
        #endregion

        #region Fields
        private List<CraigCity> _cities;
        #endregion
    }
}
