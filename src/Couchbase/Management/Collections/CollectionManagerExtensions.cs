using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Management.Collections
{
    public static class CollectionManagerExtensions
    {
        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, ScopeSpec scopeSpec)
        {
            return manager.CreateScopeAsync(scopeSpec, CreateScopeOptions.Default);
        }

        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, ScopeSpec scopeSpec, Action<CreateScopeOptions> configureOptions)
        {
            var options = new CreateScopeOptions();
            configureOptions(options);

            return manager.CreateScopeAsync(scopeSpec, options);
        }

        public static Task<ScopeSpec> GetScopeAsync(this ICouchbaseCollectionManager manager, string scopeName)
        {
            return manager.GetScopeAsync(scopeName, GetScopeOptions.Default);
        }

        public static Task<ScopeSpec> GetScopeAsync(this ICouchbaseCollectionManager manager, string scopeName, Action<GetScopeOptions> configureOptions)
        {
            var options = new GetScopeOptions();
            configureOptions(options);

            return manager.GetScopeAsync(scopeName, options);
        }

        public static Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(this ICouchbaseCollectionManager manager)
        {
            return manager.GetAllScopesAsync(GetAllScopesOptions.Default);
        }

        public static Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(this ICouchbaseCollectionManager manager, Action<GetAllScopesOptions> configureOptions)
        {
            var options = new GetAllScopesOptions();
            configureOptions(options);

            return manager.GetAllScopesAsync(options);
        }

        public static Task CreateCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec)
        {
            return manager.CreateCollectionAsync(spec, CreateCollectionOptions.Default);
        }

        public static Task CreateCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec, Action<CreateCollectionOptions> configureOptions)
        {
            var options = new CreateCollectionOptions();
            configureOptions(options);

            return manager.CreateCollectionAsync(spec, options);
        }

        public static Task DropCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec)
        {
            return manager.DropCollectionAsync(spec, DropCollectionOptions.Default);
        }

        public static Task DropCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec, Action<DropCollectionOptions> configureOptions)
        {
            var options = new DropCollectionOptions();
            configureOptions(options);

            return manager.DropCollectionAsync(spec, options);
        }

        public static Task DropScopeAsync(this ICouchbaseCollectionManager manager, string scopeName)
        {
            return manager.DropScopeAsync(scopeName, DropScopeOptions.Default);
        }

        public static Task DropScopeAsync(this ICouchbaseCollectionManager manager, string scopeName, Action<DropScopeOptions> configureOptions)
        {
            var options = new DropScopeOptions();
            configureOptions(options);

            return manager.DropScopeAsync(scopeName, options);
        }
    }
}
