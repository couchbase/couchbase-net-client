using System;

namespace Couchbase.Management.Buckets
{
    public class BucketIsNotFlushableException : Exception
    {
        public BucketIsNotFlushableException(string bucketName)
            : base($"Bucket with name {bucketName} is not flushable")
        {

        }
    }
}
