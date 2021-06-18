using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Management.Collections
{
    /// <remarks>Volatile</remarks>
    public interface ICouchbaseCollectionManager
    {
        [Obsolete("Use GetAllScopesAsync instead.")]
        Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null);

        Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null);

        Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null);

        Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null);

        [Obsolete("Use other overloaded CreateScopeAsync method that does not take a ScopeSpec instead.")]
        Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null);

        Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null);

        Task DropScopeAsync(string scopeName, DropScopeOptions? options = null);
    }
}
