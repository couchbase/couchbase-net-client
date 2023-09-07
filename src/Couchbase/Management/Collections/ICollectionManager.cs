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

        Task CreateCollectionAsync(string scopeName, string collectionName, CreateCollectionSettings settings, CreateCollectionOptions? options = null);

        [Obsolete("Use the overload with CreateCollectionSettings instead.")]
        Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null);

        Task DropCollectionAsync(string scopeName, string collectionName, DropCollectionOptions? options = null);

        [Obsolete("Use the overload that takes scope and collection names instead.")]
        Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null);

        [Obsolete("Use other overloaded CreateScopeAsync method that does not take a ScopeSpec instead.")]
        Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null);

        Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null);

        Task DropScopeAsync(string scopeName, DropScopeOptions? options = null);

        Task UpdateCollectionAsync(string scopeName, string collectionName, UpdateCollectionSettings settings, UpdateCollectionOptions? options = null);
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
