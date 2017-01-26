using System;
using Couchbase.Logging;

namespace Couchbase.Core.Diagnostics
{
    public static class TimingFactory
    {
        private static ITimingStore _store;
        private static volatile object _lockObj = new object();
        private static ILog Log = LogManager.GetLogger<OperationTimer>();

        public static Func<TimingLevel, object, IOperationTimer> GetTimer()
        {
            if (_store != null) return (level, target) => new OperationTimer(level, target, _store);
            lock (_lockObj)
            {
                if (_store == null)
                {
                    _store = new CommonLogStore(Log);
                }
            }
            return (level, target) => new OperationTimer(level, target, _store);
        }
    }
}
