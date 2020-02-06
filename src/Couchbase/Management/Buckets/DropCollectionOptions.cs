using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Buckets
{
    public class DropCollectionOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        internal TimeSpan TimeoutValue { get; set; } = TimeSpan.FromMilliseconds(75000);

        public DropCollectionOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public DropCollectionOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static DropCollectionOptions Default => new DropCollectionOptions();
    }
}
