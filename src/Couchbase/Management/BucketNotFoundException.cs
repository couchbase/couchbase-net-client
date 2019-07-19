using System;

namespace Couchbase.Management
{
    public class BucketNotFoundException : Exception
    {
        public BucketNotFoundException(string bucketName)
            : base($"Bucket with name {bucketName} does not exist")
        {

        }
    }
}