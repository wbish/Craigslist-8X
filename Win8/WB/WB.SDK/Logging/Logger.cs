using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace WB.SDK.Logging
{
    public static class Logger
    {
        static Logger()
        {
#if DEBUG
            LogSessionStart();
            _ignoredAsserts = new List<string>();
#endif
        }

#if !DEBUG
#pragma warning disable 1998
#endif

        public static async Task Assert(bool condition, string message, [CallerMemberName] string member = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
#if DEBUG
            await AssertCore(condition, "Assert", message, member, filePath, lineNumber);
#endif
        }

        public static async Task AssertNotReached(string message, [CallerMemberName] string member = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
#if DEBUG
            await AssertCore(false, "AssertNotReached", message, member, filePath, lineNumber);
#endif
        }

        public static async Task AssertNotNull(object obj, string message, [CallerMemberName] string member = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
#if DEBUG
            await AssertCore(obj != null, "AssertNotNull", message, member, filePath, lineNumber);
#endif
        }

        public static async Task AssertNull(object obj, string message, [CallerMemberName] string member = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
#if DEBUG
            await AssertCore(obj == null, "AssertNull", message, member, filePath, lineNumber);
#endif
        }

        public static async Task AssertValue<T>(T actual, T expected, string message, [CallerMemberName] string member = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
            where T : IEquatable<T>
        {
#if DEBUG
            await AssertCore(actual.Equals(expected), "AssertValue", string.Format("Actual: {0}, Expected: {1}. {2}", actual, expected, message), member, filePath, lineNumber);
#endif
        }

        private static async Task AssertCore(bool condition, string category, string message, string member, string filePath, int lineNumber)
        {
#if DEBUG
            if (!condition)
            {
                if (!_ignoreAllAsserts)
                {
                    string assert = string.Format("{0}{1}{2}{3}{4}", category, message, member, filePath, lineNumber);

                    if (!_ignoredAsserts.Contains(assert))
                    {
                        MessageDialog assertDialog = new MessageDialog(string.Format(AssertMessage, filePath, member, lineNumber, message), string.Format("{0} Failed", category));

                        assertDialog.Commands.Add(new UICommand() { Id = 0, Label = "Ignore" });
                        assertDialog.Commands.Add(new UICommand() { Id = 1, Label = "Always Ignore" });
                        //assertDialog.Commands.Add(new UICommand() { Id = 2, Label = "Ignore All Asserts" });
                        assertDialog.Commands.Add(new UICommand() { Id = 3, Label = "Debug" });

                        UICommand ret = await assertDialog.ShowAsync() as UICommand;

                        if ((int)ret.Id == 1)
                        {
                            _ignoredAsserts.Add(assert);
                        }
                        else if ((int)ret.Id == 2)
                        {
                            _ignoreAllAsserts = true;
                        }
                        else if ((int)ret.Id == 3)
                        {
                            if (Debugger.IsAttached)
                                Debugger.Break();
                            else
                                Debugger.Launch();
                        }
                    }
                }

                LogMessage(category, message);
            }
#endif
        }

#if !DEBUG
#pragma warning restore 1998
#endif

        public static void LogMessage(string category, string format, params object[] args)
        {
#if DEBUG
            LogMessage(category, string.Format(format, args));
#endif
        }

        public static void LogMessage(string category, string message)
        {
#if DEBUG
            LogRecord(string.Format("{0}: {1}", category, message));
#endif
        }

        public static void LogException(Exception ex)
        {
#if DEBUG
            LogRecord(string.Format("{0}\nStackTrace:{1}", ex.Message, ex.StackTrace));
#endif
        }

        private static void LogSessionStart()
        {
#if DEBUG
            WriteLine(new string('=', 100));
            WriteLine(string.Format("Started new session - {0}", DateTime.Now));
            WriteLine(new string('=', 100));
            WriteLine(string.Empty);
#endif
        }

        private static void LogRecord(string message)
        {
#if DEBUG
            WriteLine(string.Format("{0},{1}{2}",
                DateTime.Now,
                new string('+', _indentLevel),
                message));
#endif
        }

        private static void WriteLine(string line)
        {
#if DEBUG
            Debug.WriteLine(line);
#endif
        }

        public static void IndentLog()
        {
#if DEBUG
            ++_indentLevel;
#endif
        }

        public static void UnindentLog()
        {
#if DEBUG
            --_indentLevel;
#endif
        }

#if DEBUG
        static int _indentLevel;
        static List<string> _ignoredAsserts;
        static bool _ignoreAllAsserts;
#endif

        #region Constants
        private const string AssertMessage = @"Method: {1}

{0} @ Line [{2}]

Message:
{3}";
        #endregion
    }

    public class LoggerGroup : IDisposable
    {
        public LoggerGroup(string name)
        {
            _name = name;

            Logger.LogMessage("LogGroup", "Start '{0}'", _name);
            Logger.IndentLog();
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Logger.UnindentLog();
                    Logger.LogMessage("LogGroup", "End '{0}'", _name);
                }

                _disposed = true;
            }
        }

        bool _disposed;
        #endregion

        string _name;
    }
}
