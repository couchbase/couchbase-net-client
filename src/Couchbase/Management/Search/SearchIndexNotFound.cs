using System;

namespace Couchbase.Management.Search
{
    public class SearchIndexNotFound : Exception
    {
        public SearchIndexNotFound(string message)
            : base($"Search index with name {message} was not found.")
        { }
    }
}
