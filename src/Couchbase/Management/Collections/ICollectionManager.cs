using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

namespace Couchbase.Management.Collections
{
    public interface ICollectionManager
    {
        Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions options);

        Task<bool> ScopeExistsAsync(string scopeName, ScopeExistsOptions options);

        Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions options);

        Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions options);

        Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions options);

        Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions options);

        Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions options);

        Task DropScopeAsync(string scopeName, DropScopeOptions options);
    }
}
