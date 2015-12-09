using System;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics
{
    public static class TimingFactory
    {
        private static ITimingStore _store;
        private static volatile object _lockObj = new object();

        public static Func<TimingLevel, object, IOperationTimer> GetTimer(ILogger log)
        {
            if (_store != null) return (level, target) => new OperationTimer(level, target, _store);
            lock (_lockObj)
            {
                if (_store == null)
                {
                    _store = new CommonLogStore(log);
                }
            }
            return (level, target) => new OperationTimer(level, target, _store);
        }
    }
}
