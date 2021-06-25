using System;
using Couchbase.Query;

namespace Couchbase.Core.Retry.Query
{
    internal class QueryRequest : RequestBase
    {
        //specific to query
        public QueryOptions Options { get; set; }

        public override bool Idempotent => Options.IsReadOnly;
    }
}
