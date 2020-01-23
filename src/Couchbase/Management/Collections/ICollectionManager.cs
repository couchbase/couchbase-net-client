using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

namespace Couchbase.Management.Collections
{
    /// <remarks>Volatile</remarks>
    public interface ICollectionManager
    {
        Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions options = null);

        Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions options = null);

        Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions options = null);

        Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions options = null);

        Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions options = null);

        Task DropScopeAsync(string scopeName, DropScopeOptions options = null);
    }
}
