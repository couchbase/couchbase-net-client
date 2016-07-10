using System;
using System.Diagnostics;
using Couchbase.Core.Diagnostics;

namespace Couchbase.N1QL
{
    public class QueryTimer : IQueryTimer
    {
        private const string QueryTimingFormat = "Query Timing: {0,7:N6}ms | {1} | {2}";
        public const string NotRecorded = "NotRecorded";
        public const string QueryMustBeProvided = "Query must be provided.";
        public const string QueryStatementMustBeProvided = "Query statement must be provided.";

        public ITimingStore Store { get; private set; }
        public string ClusterElapsedTime { get; set; }

        private Stopwatch _stopwatch;
        private readonly string _statement;
        private readonly bool _enableQueryTiming;

        public QueryTimer(IQueryRequest queryRequest, ITimingStore store, bool enableQueryTiming)
        {
            Store = store;
            if (!store.Enabled || !enableQueryTiming) return;

            if (queryRequest == null)
            {
                throw new ArgumentException(QueryMustBeProvided);
            }

            if (string.IsNullOrEmpty(queryRequest.GetOriginalStatement()))
            {
                throw new ArgumentException(QueryStatementMustBeProvided);
            }

            _statement = queryRequest.GetOriginalStatement();
            _enableQueryTiming = enableQueryTiming;
            ClusterElapsedTime = NotRecorded;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (!Store.Enabled || !_enableQueryTiming) return;

            Store.Write(QueryTimingFormat, _stopwatch.Elapsed.TotalMilliseconds, ClusterElapsedTime, _statement);
            _stopwatch = null;
        }
    }
}
