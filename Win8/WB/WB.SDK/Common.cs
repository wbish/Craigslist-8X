using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;

using WB.SDK.Logging;

namespace WB.SDK
{
    public static class Utilities
    {
        static Utilities()
        {
            HtmlToTextMap.Add("quot", '"');
            HtmlToTextMap.Add("amp", '&');
            HtmlToTextMap.Add("lt", '<');
            HtmlToTextMap.Add("gt", '>');
            HtmlToTextMap.Add("apos", '\'');
            HtmlToTextMap.Add("nbsp", ' ');
        }

        public static T ExecuteRetryable<T>(Func<T> action, int retries)
        {
            if (retries < 1)
                throw new ArgumentOutOfRangeException("retries", retries, "Retries must be at least 1.");

            while (retries > 0)
            {
                --retries;

                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    Logger.LogMessage("ExecuteRetryable", "ExecuteRetryable action threw an exception. Retries remaining: {0}", retries);
                    Logger.LogException(ex);
                }
            }

            return default(T);
        }

        public static string HtmlToText(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            StringBuilder sb = new StringBuilder(html.Length);
            int start = -1;
            int parsed = 0;

            for (int i = 0; i < html.Length; ++i)
            {
                if (html[i] == '&')
                {
                    start = i;
                }
                else if (html[i] == ';' && start >= 0)
                {
                    // Named HTML entities like '&nbsp;' do not have the pound sign like '&#160;'
                    if (html[start + 1] == '#')
                    {
                        int estart = start + 2;
                        int length = i - estart;

                        if (length > 0)
                        {
                            int value = 0;
                            if (int.TryParse(html.Substring(estart, length), out value))
                            {
                                try
                                {
                                    char c = Convert.ToChar(value);
                                    sb.Append(html.Substring(parsed, start - parsed));
                                    sb.Append(c);
                                    parsed = i + 1;
                                }
                                catch (OverflowException)
                                {
                                }
                            }
                        }
                    }
                    else
                    {
                        int estart = start + 1;
                        int length = i - estart;

                        if (length > 0)
                        {
                            string entity = html.Substring(estart, length);
                            if (HtmlToTextMap.ContainsKey(entity))
                            {
                                sb.Append(html.Substring(parsed, start - parsed));
                                sb.Append(HtmlToTextMap[entity]);
                                parsed = i + 1;
                            }
                        }
                    }
                }
            }

            // Check if something was actually parsed so we do not have to substring and then create to string builder
            // string needlessly. Savings are minor, but measurable. (<5ms per 100 strings)
            if (parsed > 0)
            {
                sb.Append(html.Substring(parsed, html.Length - parsed));
                return sb.ToString();
            }
            else
            {
                return html;
            }
        }

        readonly static private Dictionary<string, char> HtmlToTextMap = new Dictionary<string, char>();

        public static async Task<StorageFile> GetPackagedFile(string folderName, string fileName)
        {
            StorageFolder installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            if (folderName != null)
            {
                StorageFolder subFolder = await installFolder.GetFolderAsync(folderName);
                return await subFolder.GetFileAsync(fileName);
            }
            else
            {
                return await installFolder.GetFileAsync(fileName);
            }
        }

        public static Task BeginAsync(this Storyboard storyboard)
        {
            System.Threading.Tasks.TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            if (storyboard == null)
                tcs.SetException(new ArgumentNullException());
            else
            {
                EventHandler<object> onComplete = null;
                onComplete = (s, e) =>
                {
                    storyboard.Completed -= onComplete;
                    tcs.SetResult(true);
                };
                storyboard.Completed += onComplete;
                storyboard.Begin();
            }
            return tcs.Task;
        }
    }

    public class RetryException : Exception
    {
    }
}
