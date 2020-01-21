using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Buckets
{
    public class FlushBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        internal TimeSpan TimeoutValue { get; set; } = TimeSpan.FromMilliseconds(75000);

        public FlushBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public FlushBucketOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static FlushBucketOptions Default => new FlushBucketOptions();
    }
}
