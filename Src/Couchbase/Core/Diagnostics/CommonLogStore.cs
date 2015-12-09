
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics
{
    public class CommonLogStore : ITimingStore
    {
        private readonly ILogger _log;
        public CommonLogStore(ILogger log)
        {
            _log = log;
        }

        public void Write(string format, params object[] args)
        {
            _log.LogInformation(string.Format(format, args));
        }

        public bool Enabled
        {
            get { return _log != null && _log.IsEnabled(LogLevel.Debug); }
        }
    }
}
