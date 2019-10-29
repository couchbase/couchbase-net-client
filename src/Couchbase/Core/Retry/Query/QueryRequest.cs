using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Query;

namespace Couchbase.Core.Retry.Query
{
    internal class QueryRequest : IRequest
    {
        public uint Attempts { get; set; }
        public bool Idempotent => Options.IsReadOnly;
        public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy(new ControlledBackoff());
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string ClientContextId { get; set; }
        public List<RetryReason> RetryReasons { get; set; } = new List<RetryReason>();
        public string Statement { get; set; }

        //specific to query
        public QueryOptions Options { get; set; }
    }
}
