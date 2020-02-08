using System;
using Couchbase.Core;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class CounterResult : MutationResult, ICounterResult
    {
        internal CounterResult(ulong value, ulong cas, TimeSpan? expiry, MutationToken? token)
            : base(cas, expiry, token)
        {
            Content = value;
        }

        public ulong Content { get; }
    }
}
