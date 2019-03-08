using System;
using Couchbase.Core;

namespace Couchbase
{
    public class MutationResult : IMutationResult
    {
        internal MutationResult(ulong cas, TimeSpan? expiration, MutationToken token)
        {
            Cas = cas;
            Expiration = expiration;
            MutationToken = token;
        }

        public ulong Cas { get; }
        public TimeSpan? Expiration { get; }
        public MutationToken MutationToken { get; set; }
    }
}
