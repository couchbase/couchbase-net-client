using Couchbase.Logging;

namespace Couchbase.Core.Diagnostics
{
    public class CommonLogStore : ITimingStore
    {
        private readonly ILog _log;
        public CommonLogStore(ILog log)
        {
            _log = log;
        }

        public void Write(string format, params object[] args)
        {
            _log.Info(format, args);
        }

        public bool Enabled
        {
            get { return _log != null && _log.IsDebugEnabled; }
        }
    }
}
