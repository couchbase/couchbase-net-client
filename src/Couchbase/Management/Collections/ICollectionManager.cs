using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

namespace Couchbase.Management.Collections
{
    public interface ICollectionManager
    {
        Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions options = null);

        Task<bool> ScopeExistsAsync(string scopeName, ScopeExistsOptions options = null);

        Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions options = null);

        Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions options = null);

        Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions options = null);

        Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions options = null);

        Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions options = null);

        Task DropScopeAsync(string scopeName, DropScopeOptions options = null);
    }
}
