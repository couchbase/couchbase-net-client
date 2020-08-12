using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal class Scope : IScope
    {
        public const string DefaultScopeName = "_default";
        public const string DefaultScopeId = "0";

        private readonly BucketBase _bucket;
        private readonly ILogger<Scope> _logger;
        private readonly ConcurrentDictionary<string, ICouchbaseCollection> _collections;
        private readonly string _queryContext;

        public Scope(ScopeDef? scopeDef, ICollectionFactory collectionFactory, BucketBase bucket, ILogger<Scope> logger)
        {
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (scopeDef != null)
            {
                Name = scopeDef.name;
                Id = scopeDef.uid;

                _collections = new ConcurrentDictionary<string, ICouchbaseCollection>(
                    scopeDef.collections
                        .Select(p => collectionFactory.Create(bucket, this, Convert.ToUInt32(p.uid, 16), p.name))
                        .ToDictionary(x => x.Name, v => v));
            }
            else
            {
                Name = DefaultScopeName;
                Id = DefaultScopeId;

                _collections = new ConcurrentDictionary<string, ICouchbaseCollection>();
                _collections.TryAdd(CouchbaseCollection.DefaultCollectionName,
                    collectionFactory.Create(bucket, this, null, CouchbaseCollection.DefaultCollectionName));
            }

            _queryContext = $"{_bucket.Name}.{Name}";
        }

        public string Id { get; }

        public string Name { get; }

        /// <inheritdoc />
        public IBucket Bucket => _bucket;

        public ICouchbaseCollection this[string name]
        {
            get
            {
                _logger.LogDebug("Fetching collection {collectionName}.", name);

                if(_collections.TryGetValue(name, out var collection))
                {
                    return collection;
                }

                //return the default bucket which will fail on first op invocation
                if (!(_bucket as IBootstrappable).IsBootstrapped)
                {
                    return _bucket.DefaultCollection();
                }

                throw new CollectionNotFoundException($"Cannot find collection {name}");
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

            return _bucket.Cluster.QueryAsync<T>(statement, options);
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
            options ??=new AnalyticsOptions();
            options.QueryContext = _queryContext;

            return _bucket.Cluster.AnalyticsQueryAsync<T>(statement, options);
        }
    }
}
