using System;

namespace Couchbase.Management
{
    public class QueryIndexNotFoundException : Exception
    {
        public QueryIndexNotFoundException(string bucketName, string indexName)
            : base($"Could not find index {indexName} on bucket {bucketName}")
        {

        }
    }
}