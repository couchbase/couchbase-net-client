using System;

namespace Couchbase.Management.Buckets
{
    public class BucketAlreadyExistsException : Exception
    {
        public BucketAlreadyExistsException(string bucketName)
            : base($"Bucket with name {bucketName} already exists")
        {

        }
    }
}
