using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Management.Collections
{
    public static class CollectionManagerExtensions
    {
        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, string scopeName)
        {
            return manager.CreateScopeAsync(scopeName, CreateScopeOptions.Default);
        }

        [Obsolete("Use other overloaded CreateScopeAsync method that does not take a ScopeSpec instead.")]
        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, ScopeSpec scopeSpec)
        {
            return manager.CreateScopeAsync(scopeSpec.Name, CreateScopeOptions.Default);
        }

        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, string scopeName, Action<CreateScopeOptions> configureOptions)
        {
            var options = new CreateScopeOptions();
            configureOptions(options);

            return manager.CreateScopeAsync(scopeName, options);
        }


        [Obsolete("Use other overloaded CreateScopeAsync method that does not take a ScopeSpec instead.")]
        public static Task CreateScopeAsync(this ICouchbaseCollectionManager manager, ScopeSpec scopeSpec, Action<CreateScopeOptions> configureOptions)
        {
            var options = new CreateScopeOptions();
            configureOptions(options);

            return manager.CreateScopeAsync(scopeSpec.Name, options);
        }

        [Obsolete("Use GetAllScopesAsync instead.")]
        public static Task<ScopeSpec> GetScopeAsync(this ICouchbaseCollectionManager manager, string scopeName)
        {
            return manager.GetScopeAsync(scopeName, GetScopeOptions.Default);
        }

        [Obsolete("Use GetAllScopesAsync instead.")]
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

        [Obsolete("Use the overload with scope and collection names instead.")]
        public static Task CreateCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec)
        {
            return manager.CreateCollectionAsync(spec, CreateCollectionOptions.Default);
        }

        public static Task CreateCollectionAsync(this ICouchbaseCollectionManager manager, string scopeName,
            string collectionName, CreateCollectionSettings settings, Action<CreateCollectionOptions> configureOptions)
        {
            var options = new CreateCollectionOptions();
            configureOptions(options);

            return manager.CreateCollectionAsync(scopeName, collectionName, settings, options);
        }

        [Obsolete("Use the overload with CreateCollectionSettings instead.")]
        public static Task CreateCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec, Action<CreateCollectionOptions> configureOptions)
        {
            var options = new CreateCollectionOptions();
            configureOptions(options);

            return manager.CreateCollectionAsync(spec, options);
        }

        [Obsolete("Use the overload that uses scope and collection names instead.")]
        public static Task DropCollectionAsync(this ICouchbaseCollectionManager manager, CollectionSpec spec)
        {
            return manager.DropCollectionAsync(spec, DropCollectionOptions.Default);
        }

        public static Task DropCollectionAsync(this ICouchbaseCollectionManager manager, string scopeName,
            string collectionName, Action<DropCollectionOptions> configureOptions)
        {
            var options = new DropCollectionOptions();
            configureOptions(options);

            return manager.DropCollectionAsync(scopeName, collectionName, options);
        }

        [Obsolete("Use the overload that uses scope and collection names instead.")]
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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
