using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using Windows.UI.Xaml.Data;

namespace WB.Craigslist8X.Common
{
    /// <summary>
    /// Generic extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        public static void Copy<T>(this ObservableCollection<T> target, IEnumerable<T> source)
        {
            if (target == null)
            {
                target = new ObservableCollection<T>(source);
            }
            else
            {
                target.Clear();

                foreach (T item in source)
                {
                    target.Add(item);
                }
            }
        }
    }
}