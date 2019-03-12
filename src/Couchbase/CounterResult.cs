using System;
using Couchbase.Core;

namespace Couchbase
{
    public class CounterResult : MutationResult, ICounterResult
    {
        internal CounterResult(ulong value, ulong cas, TimeSpan? expiration, MutationToken token)
            : base(cas, expiration, token)
        {
            Content = value;
        }

        public ulong Content { get; }
    }
}
