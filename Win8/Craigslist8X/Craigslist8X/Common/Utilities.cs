using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace WB.Craigslist8X.Common
{
    public static class Utilities
    {
        public static async Task<MemoryStream> GetImageStream(Uri uri)
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(uri))
            {
                if (response.IsSuccessStatusCode)
                {
                    return new MemoryStream(await response.Content.ReadAsByteArrayAsync());
                }
                else
                {
                    return null;
                }
            }
        }

        public static T GetVisualChild<T>(DependencyObject referenceVisual) where T : DependencyObject
        {
            DependencyObject child = null;
            for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(referenceVisual); i++)
            {
                child = VisualTreeHelper.GetChild(referenceVisual, i) as DependencyObject;
                if (child != null && (child.GetType() == typeof(T)))
                {
                    break;
                }
                else if (child != null)
                {
                    child = GetVisualChild<T>(child);
                    if (child != null && (child.GetType() == typeof(T)))
                    {
                        break;
                    }
                }
            }
            return child as T;
        }

        public static void ExecuteNotification(string title, string message)
        {
            var template = ToastTemplateType.ToastText02;
            var content = ToastNotificationManager.GetTemplateContent(template);
            content.SelectSingleNode("/toast/visual/binding/text[@id='1']").InnerText = title;
            content.SelectSingleNode("/toast/visual/binding/text[@id='2']").InnerText = message;

            ToastNotification toast = new ToastNotification(content);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        
        public static Rect GetElementRect(FrameworkElement element)
        {
            GeneralTransform buttonTransform = element.TransformToVisual(null);
            Point point = buttonTransform.TransformPoint(new Point());
            return new Rect(point, new Size(element.ActualWidth, element.ActualHeight));
        }

        public static async Task<string> LoadFileAsync(this StorageFile file)
        {
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            using (var readStream = stream.GetInputStreamAt(0))
            using (DataReader reader = new DataReader(readStream))
            {
                uint bytesLoaded = await reader.LoadAsync((uint)stream.Size);
                return reader.ReadString(bytesLoaded);
            }
        }

        public static async Task SaveFileAsync(this StorageFile file, string content)
        {
            using (var raStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            using (var outStream = raStream.GetOutputStreamAt(0))
            using (DataWriter writer = new DataWriter(outStream))
            {
                writer.WriteString(content);

                await writer.StoreAsync();
                await outStream.FlushAsync();
            }
        }
    }
}
