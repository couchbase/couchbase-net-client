using System;

namespace Couchbase.Management
{
    public class BucketAlreadyExistsException : Exception
    {
        public BucketAlreadyExistsException(string bucketName)
            : base($"Bucket with name {bucketName} already exists")
        {

        }
    }
}