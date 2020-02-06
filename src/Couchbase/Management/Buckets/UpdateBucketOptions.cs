using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Buckets
{
    public class UpdateBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        internal TimeSpan TimeoutValue { get; set; } = TimeSpan.FromMilliseconds(75000);

        public UpdateBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public UpdateBucketOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static UpdateBucketOptions Default => new UpdateBucketOptions();
    }
}
