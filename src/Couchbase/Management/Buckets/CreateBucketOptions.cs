using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Buckets
{
    public class CreateBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        internal TimeSpan TimeoutValue { get; set; } = TimeSpan.FromMilliseconds(75000);

        public CreateBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public CreateBucketOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static CreateBucketOptions Default => new CreateBucketOptions();
    }
}
