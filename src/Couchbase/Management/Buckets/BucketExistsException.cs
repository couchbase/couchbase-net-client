using System;

namespace Couchbase.Management.Buckets
{
    public class BucketExistsException : Exception
    {
        public BucketExistsException(string bucketName)
            : base($"Bucket with name {bucketName} already exists")
        {

        }
    }
}
