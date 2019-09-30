using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Query
{
    public interface IQueryIndexManager
    {
        Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(string bucketName, GetAllQueryIndexOptions options);

        Task CreateIndexAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options);

        Task CreatePrimaryIndexAsync(string bucketName, CreatePrimaryQueryIndexOptions options);

        Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions options);

        Task DropPrimaryIndexAsync(string bucketName, DropPrimaryQueryIndexOptions options);

        Task BuildDeferredIndexesAsync(string bucketName, BuildDeferredQueryIndexOptions options);

        Task WatchIndexesAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions options);
    }
}
