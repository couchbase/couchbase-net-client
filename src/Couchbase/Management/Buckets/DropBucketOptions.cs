using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Buckets
{
    public class DropBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        internal TimeSpan TimeoutValue { get; set; } = TimeSpan.FromMilliseconds(75000);

        public DropBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public DropBucketOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static DropBucketOptions Default => new DropBucketOptions();
    }
}
