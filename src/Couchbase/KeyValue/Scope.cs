using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Query;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal class Scope : IScope, IInternalScope
    {
        public const string DefaultScopeName = "_default";
        public const string DefaultScopeId = "0";

        private readonly BucketBase _bucket;
        private readonly ILogger<Scope> _logger;
        private readonly ConcurrentDictionary<string, ICouchbaseCollection> _collections;
        private readonly string _queryContext;
        private readonly ICollectionFactory _collectionFactory;
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _operationConfigurator;

        public Scope(string name, string sid, BucketBase bucket, ICollectionFactory collectionFactory, ILogger<Scope> logger, IRequestTracer tracer, IOperationConfigurator operationConfigurator)
        {
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _collectionFactory =  collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));

            Name = name;
            Id = sid ?? throw new ArgumentNullException(nameof(sid));
            _bucket = bucket;
            _logger = logger;
            _queryContext = $"{_bucket.Name}.{Name}";
            _collections = new ConcurrentDictionary<string, ICouchbaseCollection>();
        }

        public Scope(ScopeDef? scopeDef, ICollectionFactory collectionFactory, BucketBase bucket, ILogger<Scope> logger, IRequestTracer tracer, IOperationConfigurator operationConfigurator)
        {
            _collectionFactory = collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));

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

        [Obsolete("Use asynchronous equivalent instead.")]
        public ICouchbaseCollection this[string name]
        {
            get
            {
                _logger.LogDebug("Fetching collection {collectionName}.", name);

                if (_collections.TryGetValue(name, out var collection))
                {
                    return collection;
                }

                //return the default bucket which will fail on first op invocation
                if (!(_bucket as IBootstrappable).IsBootstrapped)
                {

#pragma warning disable 618
            return _bucket.DefaultCollection();
#pragma warning restore 618
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
        [Obsolete("Use asynchronous equivalent instead.")]
        public ICouchbaseCollection Collection(string collectionName)
        {
            return this[collectionName];
        }

        public async ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            _logger.LogDebug("Fetching collection {collectionName}.", collectionName);

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collectionName));
            }

            // we have a cached collection
            // ReSharper disable once AssignNullToNotNullAttribute
            if (_collections.TryGetValue(collectionName, out var collection))
            {
                return await Task.FromResult(collection).ConfigureAwait(false);
            }

            var clusterNode = _bucket.Nodes.GetRandom();
            if (clusterNode == null)
            {
                ThrowHelper.ThrowNodeUnavailableException(
                    $"Could not select a node while fetching collection {collectionName} for scope {Name} and bucket {_bucket.Name}");
            }

            // ReSharper disable once PossibleNullReferenceException
            var collectionIdentifier = await GetCidAsync($"{Name}.{collectionName}").ConfigureAwait(false);
            collection = _collectionFactory.Create(_bucket, this, collectionIdentifier, collectionName);
            if (_collections.TryAdd(collectionName, collection))
            {
                return collection;
            }

            //return the default collection which will fail on first op invocation
            if (!(_bucket as IBootstrappable).IsBootstrapped)
            {
                //temp pragma!
#pragma warning disable 618
                return _bucket.DefaultCollection();
#pragma warning restore 618
            }

            throw new CollectionNotFoundException(collectionName);
        }

        public async Task<uint?> GetCidAsync(string fullyQualifiedName)
        {
            using var rootSpan = RootSpan(OperationNames.GetCid);
            using var getCid = new GetCid
            {
                Transcoder = _bucket.Context.GlobalTranscoder,
                Key = fullyQualifiedName,
                Opaque = SequenceGenerator.GetNext(),
                Content = null,
                Span = rootSpan,
            };

            _operationConfigurator.Configure(getCid);
            await _bucket.RetryAsync(getCid).ConfigureAwait(false);
            var resultWithValue = getCid.GetValue();
            return resultWithValue!.GetValueOrDefault();
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

        private IInternalSpan RootSpan(string operation) =>
            _tracer.RootSpan(RequestTracing.ServiceIdentifier.Kv, operation);
    }
}
