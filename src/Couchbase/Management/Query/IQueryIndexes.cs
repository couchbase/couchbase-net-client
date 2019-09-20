using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Query
{
    public interface IQueryIndexes
    {
        Task<IEnumerable<QueryIndex>> GetAllAsync(string bucketName, GetAllQueryIndexOptions options);

        Task CreateAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options);

        Task CreatePrimaryAsync(string bucketName, CreatePrimaryQueryIndexOptions options);

        Task DropAsync(string bucketName, string indexName, DropQueryIndexOptions options);

        Task DropPrimaryAsync(string bucketName, DropPrimaryQueryIndexOptions options);

        Task BuildDeferredAsync(string bucketName, BuildDeferredQueryIndexOptions options);

        Task WatchAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions options);
    }
}
