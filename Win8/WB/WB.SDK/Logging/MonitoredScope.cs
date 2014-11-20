using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WB.SDK.Logging
{
    public class MonitoredScope : LoggerGroup
    {
        public MonitoredScope(string name)
            : base(name)
        {
            this._start = TimeSpan.FromTicks(System.Diagnostics.Stopwatch.GetTimestamp());
            this._name = name;
        }

        public MonitoredScope(string name, params object[] args)
            : this(string.Format(name, args))
        {
        }

        #region IDisposable
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    TimeSpan stop = TimeSpan.FromTicks(System.Diagnostics.Stopwatch.GetTimestamp());
                    Logger.LogMessage("MonitoredScope", "Finished {0} in {1} milliseconds", this._name, (stop - this._start).TotalMilliseconds);
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        bool _disposed;
        string _name;
        TimeSpan _start;
        #endregion
    }

}
