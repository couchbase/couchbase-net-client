using System;
using Couchbase.Core;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class MutationResult : IMutationResult
    {
        internal MutationResult(ulong cas, TimeSpan? expiry, MutationToken? token)
        {
            Cas = cas;
            Expiry = expiry;
            MutationToken = token ?? MutationToken.Empty;
        }

        public ulong Cas { get; }
        public TimeSpan? Expiry { get; }
        public MutationToken MutationToken { get; set; }
    }
}
