using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace WB.Craigslist8X.ViewModel
{
    /// <summary>
    /// Value converter that translates true and false to characters representings favorite status
    /// </summary>
    public sealed class BooleanToFavoriteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool && (bool)value) ? new string('\uE0A5', 1) : new string('\uE006', 1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Value converter that translates true and false to characters representings favorite status
    /// </summary>
    public sealed class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool && (bool)value) ? new SolidColorBrush(TrueColor) : new SolidColorBrush(FalseColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((SolidColorBrush)value).Color.Equals(TrueColor) ? true : false;
        }

        public Color TrueColor
        {
            get;
            set;
        }

        public Color FalseColor
        {
            get;
            set;
        }
    }
}
