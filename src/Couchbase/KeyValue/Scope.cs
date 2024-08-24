using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.Utils;
using Couchbase.Management.Eventing;
using Couchbase.Management.Eventing.Internal;
using Couchbase.Management.Search;
using Couchbase.Query;
using Couchbase.Search;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal sealed class Scope : IScope
    {
        public const string DefaultScopeName = "_default";
        private readonly BucketBase _bucket;
        private readonly ILogger<Scope> _logger;
        private readonly ConcurrentDictionary<string, ICouchbaseCollection> _collections;
        private readonly string _queryContext;
        private readonly ICollectionFactory _collectionFactory;

        public Scope(string name, BucketBase bucket, ICollectionFactory collectionFactory, ILogger<Scope> logger, IEventingFunctionManagerFactory eventingFunctionManagerFactory)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _collectionFactory = collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));

            _collections = new ConcurrentDictionary<string, ICouchbaseCollection>();
            _queryContext = Utils.QueryContext.Create(_bucket.Name.EscapeIfRequired(), name.EscapeIfRequired());
            IsDefaultScope = name == DefaultScopeName;
            SearchIndexes = new ScopedSearchIndexManagerWrapper(this);
            EventingFunctions = eventingFunctionManagerFactory.CreateScoped(this);
        }

        /// <summary>
        /// Internal seam for unit testing
        /// </summary>
        internal string QueryContext => _queryContext;

        public string Name { get; }

        /// <inheritdoc />
        public IBucket Bucket => _bucket;

        /// <inheritdoc />
        public bool IsDefaultScope { get; }

        public ICouchbaseCollection this[string name]
        {
            get
            {
                _logger.LogTrace("Fetching collection {collectionName}.", name);
                return _collections.GetOrAdd(name,
                    key => _collectionFactory.Create(_bucket, this, key));
            }
        }

        /// <summary>
        /// Returns a given collection by name.
        /// </summary>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        /// <remarks>Volatile</remarks>
        public ICouchbaseCollection Collection(string collectionName)
        {
            return this[collectionName];
        }

        public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            _logger.LogTrace("Fetching collection {collectionName}.", collectionName);
            return new ValueTask<ICouchbaseCollection>(Collection(collectionName));
        }

        /// <summary>
        /// Collection N1QL querying
        /// </summary>
        /// <typeparam name="T">The Type of the row returned.</typeparam>
        /// <param name="statement">The required statement to execute.</param>
        /// <param name="options">Optional parameters.</param>
        /// <returns>A <see cref="IQueryResult{T}"/> which can be enumerated.</returns>
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = default)
        {
            options ??= new QueryOptions();
            options.QueryContext = _queryContext;
            options.ScopeName = Name;
            options.BucketName = _bucket.Name;

            return _bucket.Cluster.QueryAsync<T>(statement, options);
        }

        /// <inheritdoc />
        public async Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, SearchOptions? options = default)
        {
            options ??= new();
            options.Scope(this.Name);
            searchRequest = searchRequest with { Scope = this };
            return await this.Bucket.Cluster.SearchAsync(searchIndexName, searchRequest, options).ConfigureAwait(false);
        }

        /// <summary>
        /// Collection analytics querying
        /// </summary>
        /// <typeparam name="T">The Type of the row returned.</typeparam>
        /// <param name="statement">The required statement to execute.</param>
        /// <param name="options">Optional parameters.</param>
        /// <returns>A <see cref="IAnalyticsResult{T}"/> which can be enumerated.</returns>
        public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            options ??= new AnalyticsOptions
            {
                QueryContext = _queryContext
            };
            options.BucketName = _bucket.Name;
            options.ScopeName = Name;

            return _bucket.Cluster.AnalyticsQueryAsync<T>(statement, options);
        }

        /// <inheritdoc />
        public IEventingFunctionManager EventingFunctions { get; }

        /// <inheritdoc />
        public ISearchIndexManager SearchIndexes { get; }

        private class ScopedSearchIndexManagerWrapper : ISearchIndexManager
        {
            private readonly IScope _scope;
            private readonly ISearchIndexManager _manager;

            public ScopedSearchIndexManagerWrapper(IScope scope)
            {
                _scope = scope;
                var cluster = scope.Bucket.Cluster;
                _manager = cluster.SearchIndexes;
            }

            public Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.GetIndexAsync(indexName, options, scope ?? _scope);

            public Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions? options = null,
                IScope? scope = null)
                => _manager.GetAllIndexesAsync(options, scope ?? _scope);

            public Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.UpsertIndexAsync(indexDefinition, options, scope ?? _scope);

            public Task DropIndexAsync(string indexName, DropSearchIndexOptions? options = null, IScope? scope = null)
                => _manager.DropIndexAsync(indexName, options, scope ?? _scope);

            public Task<int> GetIndexedDocumentsCountAsync(string indexName,
                GetSearchIndexDocumentCountOptions? options = null,
                IScope? scope = null)
                => _manager.GetIndexedDocumentsCountAsync(indexName, options, scope ?? _scope);

            public Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.PauseIngestAsync(indexName, options, scope ?? _scope);

            public Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.ResumeIngestAsync(indexName, options, scope ?? _scope);

            public Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.AllowQueryingAsync(indexName, options, scope ?? _scope);

            public Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.DisallowQueryingAsync(indexName, options, scope ?? _scope);

            public Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.FreezePlanAsync(indexName, options, scope ?? _scope);

            public Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions? options = null,
                IScope? scope = null)
                => _manager.UnfreezePlanAsync(indexName, options, scope ?? _scope);
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
