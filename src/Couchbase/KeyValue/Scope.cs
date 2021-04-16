using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal class Scope : IScope
    {
        public const string DefaultScopeName = "_default";
        private readonly BucketBase _bucket;
        private readonly ILogger<Scope> _logger;
        private readonly ConcurrentDictionary<string, ICouchbaseCollection> _collections;
        private readonly string _queryContext;
        private readonly ICollectionFactory _collectionFactory;

        public Scope(string name, BucketBase bucket, ICollectionFactory collectionFactory, ILogger<Scope> logger)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _collectionFactory = collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));

            _collections = new ConcurrentDictionary<string, ICouchbaseCollection>();
            _queryContext = $"{_bucket.Name}.{Name}";
        }

        public string Name { get; }

        /// <inheritdoc />
        public IBucket Bucket => _bucket;

        public ICouchbaseCollection this[string name]
        {
            get
            {
                _logger.LogDebug("Fetching collection {collectionName}.", name);
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
            _logger.LogDebug("Fetching collection {collectionName}.", collectionName);
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
            options.BucketName = _bucket.Name;
            options.ScopeName = Name;

            return _bucket.Cluster.AnalyticsQueryAsync<T>(statement, options);
        }
    }
}
