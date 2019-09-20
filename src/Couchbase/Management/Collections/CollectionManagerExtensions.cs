using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

namespace Couchbase.Management.Collections
{
    public static class CollectionManagerExtensions
    {
        public static Task CreateScopeAsync(this ICollectionManager manager, ScopeSpec scopeSpec)
        {
            return manager.CreateScopeAsync(scopeSpec, CreateScopeOptions.Default);
        }

        public static Task CreateScopeAsync(this ICollectionManager manager, ScopeSpec scopeSpec, Action<CreateScopeOptions> configureOptions)
        {
            var options = new CreateScopeOptions();
            configureOptions(options);

            return manager.CreateScopeAsync(scopeSpec, options);
        }

        public static Task<bool> ScopeExistsAsync(this ICollectionManager manager, string scopeName)
        {
            return manager.ScopeExistsAsync(scopeName, ScopeExistsOptions.Default);
        }

        public static Task<bool> ScopeExistsAsync(this ICollectionManager manager, string scopeName, Action<ScopeExistsOptions> configureOptions)
        {
            var options = new ScopeExistsOptions();
            configureOptions(options);

            return manager.ScopeExistsAsync(scopeName, options);
        }

        public static Task<ScopeSpec> GetScopeAsync(this ICollectionManager manager, string scopeName)
        {
            return manager.GetScopeAsync(scopeName, GetScopeOptions.Default);
        }

        public static Task<ScopeSpec> GetScopeAsync(this ICollectionManager manager, string scopeName, Action<GetScopeOptions> configureOptions)
        {
            var options = new GetScopeOptions();
            configureOptions(options);

            return manager.GetScopeAsync(scopeName, options);
        }

        public static Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(this ICollectionManager manager)
        {
            return manager.GetAllScopesAsync(GetAllScopesOptions.Default);
        }

        public static Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(this ICollectionManager manager, Action<GetAllScopesOptions> configureOptions)
        {
            var options = new GetAllScopesOptions();
            configureOptions(options);

            return manager.GetAllScopesAsync(options);
        }

        public static Task CreateCollectionAsync(this ICollectionManager manager, CollectionSpec spec)
        {
            return manager.CreateCollectionAsync(spec, CreateCollectionOptions.Default);
        }

        public static Task CreateCollectionAsync(this ICollectionManager manager, CollectionSpec spec, Action<CreateCollectionOptions> configureOptions)
        {
            var options = new CreateCollectionOptions();
            configureOptions(options);

            return manager.CreateCollectionAsync(spec, options);
        }

        public static Task<bool> CollectionExistsAsync(this ICollectionManager manager, CollectionSpec spec)
        {
            return manager.CollectionExistsAsync(spec, CollectionExistsOptions.Default);
        }

        public static Task<bool> CollectionExistsAsync(this ICollectionManager manager, CollectionSpec spec, Action<CollectionExistsOptions> configureOptions)
        {
            var options = new CollectionExistsOptions();
            configureOptions(options);

            return manager.CollectionExistsAsync(spec, options);
        }

        public static Task DropCollectionAsync(this ICollectionManager manager, CollectionSpec spec)
        {
            return manager.DropCollectionAsync(spec, DropCollectionOptions.Default);
        }

        public static Task DropCollectionAsync(this ICollectionManager manager, CollectionSpec spec, Action<DropCollectionOptions> configureOptions)
        {
            var options = new DropCollectionOptions();
            configureOptions(options);

            return manager.DropCollectionAsync(spec, options);
        }

        public static Task DropScopeAsync(this ICollectionManager manager, string scopeName)
        {
            return manager.DropScopeAsync(scopeName, DropScopeOptions.Default);
        }

        public static Task DropScopeAsync(this ICollectionManager manager, string scopeName, Action<DropScopeOptions> configureOptions)
        {
            var options = new DropScopeOptions();
            configureOptions(options);

            return manager.DropScopeAsync(scopeName, options);
        }
    }
}
