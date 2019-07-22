using System;

namespace Couchbase.Management
{
    public class QueryIndexAlreadyExistsException : Exception
    {
        public QueryIndexAlreadyExistsException(string bucketName, string indexName)
            : base($"Could not create index {indexName} on bucket {bucketName} because it already exists")
        {

        }
    }
}