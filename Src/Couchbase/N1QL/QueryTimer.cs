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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
