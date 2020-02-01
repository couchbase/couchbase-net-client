using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Query
{
    public interface IQueryIndexManager
    {
        Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(string bucketName, GetAllQueryIndexOptions? options = null);

        Task CreateIndexAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions? options = null);

        Task CreatePrimaryIndexAsync(string bucketName, CreatePrimaryQueryIndexOptions? options = null);

        Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions? options = null);

        Task DropPrimaryIndexAsync(string bucketName, DropPrimaryQueryIndexOptions? options = null);

        Task BuildDeferredIndexesAsync(string bucketName, BuildDeferredQueryIndexOptions? options = null);

        Task WatchIndexesAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions? options = null);
    }
}
