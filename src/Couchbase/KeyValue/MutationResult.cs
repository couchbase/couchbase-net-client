using System;
using Couchbase.Core;

namespace Couchbase.KeyValue
{
    internal class MutationResult : IMutationResult
    {
        internal MutationResult(ulong cas, TimeSpan? expiry, MutationToken token)
        {
            Cas = cas;
            Expiry = expiry;
            MutationToken = token;
        }

        public ulong Cas { get; }
        public TimeSpan? Expiry { get; }
        public MutationToken MutationToken { get; set; }
    }
}
