 using System;
 using System.Collections.Concurrent;
 using System.Collections.Generic;
 using System.Linq;
 using System.Net.Http;
 using System.Runtime.CompilerServices;
 using System.Security.Authentication;
 using System.Threading;
 using System.Threading.Tasks;
 using Couchbase.Analytics;
 using Couchbase.Logging;
 using Couchbase.Annotations;
 using Couchbase.Authentication;
 using Couchbase.Configuration;
 using Couchbase.Configuration.Client;
 using Couchbase.Configuration.Server.Providers;
 using Couchbase.Core;
 using Couchbase.Core.Buckets;
 using Couchbase.Core.Transcoders;
 using Couchbase.IO.Converters;
 using Couchbase.IO.Operations;
 using Couchbase.Management;
 using Couchbase.N1QL;
 using Couchbase.Search;
 using Couchbase.Views;
 using Couchbase.Utils;

namespace Couchbase
{
    /// <summary>
    /// Represents an in-memory bucket for storing Key/Value pairs. Most often used as a distributed cache.
    /// </summary>
    /// <seealso cref="Couchbase.Core.IBucket" />
    /// <seealso cref="Couchbase.Configuration.Server.Providers.IConfigObserver" />
    /// <seealso cref="Couchbase.IRefCountable" />
    public class MemcachedBucket : IBucket, IConfigObserver, IRefCountable
    {
        private static readonly ILog Log = LogManager.GetLogger<MemcachedBucket>();
        private readonly IClusterController _clusterController;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private readonly IByteConverter _converter;
        private readonly ITypeTranscoder _transcoder;
        private readonly uint _operationLifespanTimeout;
        private MemcachedRequestExecuter _requestExecuter;
        private readonly ConcurrentDictionary<uint, IOperation> _pending = new ConcurrentDictionary<uint, IOperation>();
        private IClusterCredentials _credentials;

        /// <summary>
        /// Used for reference counting instances so that <see cref="IDisposable.Dispose"/> is only called by the last instance.
        /// </summary>
        private static readonly ConditionalWeakTable<IDisposable, RefCount> RefCounts =
            new ConditionalWeakTable<IDisposable, RefCount>();


        [UsedImplicitly]
        private sealed class RefCount
        {
            public int Count;
        }

        internal MemcachedBucket(IClusterController clusterController, string bucketName, IByteConverter converter,
            ITypeTranscoder transcoder, IClusterCredentials credentials)
        {
            _clusterController = clusterController;
            _converter = converter;
            _transcoder = transcoder;
            Name = bucketName;

            //extract the default operation lifespan timeout from configuration.
            BucketConfiguration bucketConfig;
            _operationLifespanTimeout = _clusterController.Configuration.BucketConfigs.TryGetValue(bucketName,
                out bucketConfig)
                ? bucketConfig.DefaultOperationLifespan
                : _clusterController.Configuration.DefaultOperationLifespan;

            _credentials = credentials;
        }

        /// <summary>
        /// The Bucket's name. You can view this from the Couchbase Management Console.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns type of the bucket. In this implementation the value is constant: Memcached.
        /// </summary>
        public BucketTypeEnum BucketType
        {
            get { return BucketTypeEnum.Memcached; }
        }

        /// <summary>
        /// Returns the <see cref="ICluster"/> that this bucket belongs to
        /// </summary>
        public ICluster Cluster
        {
            get { return _clusterController != null ? _clusterController.Cluster : null; }
        }

        /// <summary>
        /// Creates a <see cref="IBucketManager" /> instance for managing buckets using the <see cref="IClusterCredentials" /> for authentication.
        /// </summary>
        /// <returns>
        /// A <see cref="IBucketManager" /> instance.
        /// </returns>
        /// <exception cref="AuthenticationException">
        /// No credentials found.
        /// </exception>
        public IBucketManager CreateManager()
        {
            if (_credentials == null)
            {
                throw new AuthenticationException("No credentials found.");
            }

            var clusterCreds = _credentials.GetCredentials(AuthContext.ClusterMgmt).FirstOrDefault();
            if (clusterCreds.Key == null || clusterCreds.Value == null)
            {
                throw new AuthenticationException("No credentials found.");
            }
            return CreateManager(clusterCreds.Key, clusterCreds.Value);
        }

        /// <summary>
        /// Returns true if bucket is using SSL encryption between the client and the server.
        /// </summary>
        /// <remarks>If the server is not available (<see cref="ServerUnavailableException"/>), will default to false.</remarks>
        public bool IsSecure
        {
            get
            {
                try
                {
                    var server = _configInfo.GetServer();
                    return server.IsSecure;
                }
                catch (ServerUnavailableException e)
                {
                    Log.Info("Default to IsSecure false because of {0}", e);
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the configuration for the bucket
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        BucketConfiguration IBucket.Configuration
        {
            get { return _configInfo.ClientConfig.BucketConfigs[Name]; }
        }

        /// <summary>
        /// Gets a value indicating whether enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports enhanced durability and it is enabled; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>Note this will always be false for Memcached buckets.</remarks>
        public bool SupportsEnhancedDurability { get { return false; } }

        /// <summary>
        /// Called when a configuration update has occurred from the server.
        /// </summary>
        /// <param name="configInfo">The new configuration</param>
        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Log.Info("Config updated old/new: {0}, {1}",
                _configInfo != null ? _configInfo.BucketConfig.Rev : 0, configInfo.BucketConfig.Rev);
            Interlocked.Exchange(ref _configInfo, configInfo);
            Interlocked.Exchange(ref _requestExecuter,
                new MemcachedRequestExecuter(_clusterController, _configInfo, Name, _pending));
        }

        /// <summary>
        /// Checks for the existance of a given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists.</returns>
        public bool Exists(string key)
        {
            var observe = new Observe(key, null,  _transcoder, _operationLifespanTimeout);
            var result = _requestExecuter.SendWithRetry(observe);
            return result.Success && result.Value.KeyState != KeyState.NotFound;
        }

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>A <see cref="Task{boolean}"/> object representing the asynchronous operation.</returns>
        public async Task<bool> ExistsAsync(string key)
        {
            var observe = new Observe(key, null,  _transcoder, _operationLifespanTimeout);
            var result = await _requestExecuter.SendWithRetryAsync(observe).ContinueOnAnyContext();
            return result.Success && result.Value.KeyState != KeyState.NotFound;
        }

        public ObserveResponse Observe(string key, ulong cas, bool remove, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        public IOperationResult Touch(string key, TimeSpan expiration)
        {
            var touch = new Touch(key, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetry(touch);
        }

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        public Task<IOperationResult> TouchAsync(string key, TimeSpan expiration)
        {
            var touch = new Touch(key, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetryAsync(touch);
        }


        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document)
        {
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl());
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value)
        {
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration)
        {
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration)
        {
            return Upsert(key, value, expiration.ToTtl());
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas)
        {
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration)
        {
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas,
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return Upsert(key, value, cas, expiration.ToTtl());
        }

        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");

        }

        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (items != null && items.Count > 0)
            {
                var keys = items.Keys.ToList();
                var partitionar = Partitioner.Create(0, items.Count());
                Parallel.ForEach(partitionar, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var value = items[key];
                        var result = Upsert(key, value);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (items != null && items.Count > 0)
            {
                var keys = items.Keys.ToList();
                var partitionar = Partitioner.Create(0, items.Count());
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var value = items[key];
                        var result = Upsert(key, value);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options,
            int rangeSize)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (items != null && items.Count > 0)
            {
                var keys = items.Keys.ToList();
                var partitionar = Partitioner.Create(0, items.Count(), rangeSize);
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var value = items[key];
                        var result = Upsert(key, value);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document)
        {
            var result = Replace(document.Id, document.Content, document.Cas, document.Expiry.ToTtl());
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas)
        {
            var operation = new Replace<T>(key, value, cas, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a value for a key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value)
        {
            var operation = new Replace<T>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration)
        {
            var operation = new Replace<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration)
        {
            return Replace(key, value, expiration.ToTtl());
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing <see cref="IDocumentResult{T}" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration)
        {
            var operation = new Replace<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return Replace(key, value, cas, expiration.ToTtl());
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document)
        {
            var result = Insert(document.Id, document.Content, document.Expiry.ToTtl());
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <returns></returns>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(InsertAsync(doc)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value)
        {
            var operation = new Add<T>(key, value, null,  _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration)
        {
            var operation = new Add<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration)
        {
            return Insert(key, value, expiration.ToTtl());
        }

        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IOperationResult Remove<T>(IDocument<T> document)
        {
            return Remove(document.Id);
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key)
        {
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas)
        {
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <returns>
        /// A <see cref="Dictionary{k, v}" /> of the keys sent and the <see cref="IOperationResult{T}" /> result.
        /// </returns>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys)
        {
            var results = new ConcurrentDictionary<string, IOperationResult>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count());
                Parallel.ForEach(partitionar, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Remove(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions" /> instance with the options for the given operation.</param>
        /// <returns>
        /// A <see cref="Dictionary{k, v}" /> of the keys sent and the <see cref="IOperationResult{T}" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options)
        {
            var results = new ConcurrentDictionary<string, IOperationResult>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count());
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Remove(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>
        /// A <see cref="Dictionary{k, v}" /> of the keys sent and the <see cref="IOperationResult{T}" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize)
        {
            var results = new ConcurrentDictionary<string, IOperationResult>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count(), rangeSize);
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Remove(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult> RemoveAsync(string key)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        public IDocumentResult<T> GetDocument<T>(string id)
        {
            var result = Get<T>(id);
            return new DocumentResult<T>(result);
        }

        /// <summary>
        /// Gets a list of documents by there given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="ids">The documents primary keys.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> array representing the asynchronous operation results.
        /// </returns>
            public Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            ids.ToList().ForEach(id => tasks.Add(GetDocumentAsync<T>(id)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets a value for a given key from a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            var operation = new Get<T>(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the key's value.</returns>
        public IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration)
        {
            var operation = new GetT<T>(key, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        public Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration)
        {
            var operation = new GetT<T>(key, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> with the key's document.</returns>
        public IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration)
        {
            var result = GetAndTouch<T>(key, expiration);
            return new DocumentResult<T>(result);
        }

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        public Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            var result = GetAndTouchAsync<T>(key, expiration);
            tcs.SetResult(new DocumentResult<T>(result.Result));
            return tcs.Task;
        }

        public IOperationResult<T> GetFromReplica<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count());
                Parallel.ForEach(partitionar, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Get<T>(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options"></param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count());
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Get<T>(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options"></param>
        /// <param name="rangeSize"></param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options,
            int rangeSize)
        {
            var results = new ConcurrentDictionary<string, IOperationResult<T>>();
            if (keys != null && keys.Count > 0)
            {
                var partitionar = Partitioner.Create(0, keys.Count(), rangeSize);
                Parallel.ForEach(partitionar, options, (range, loopstate) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var key = keys[i];
                        var result = Get<T>(key);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        public IOperationResult<T> GetWithLock<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> GetAndLock<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Unlock(string key, ulong cas)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<ulong> Increment(string key)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<ulong> Increment(string key, ulong delta)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial)
        {
            //infinite - there is also a 'special' value -1: 'don't create if missing'
            const uint expiration = 0;

            return Increment(key, delta, initial, expiration);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration)
        {
            var operation = new Increment(key, initial, delta, expiration, null, _transcoder,
                _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return Increment(key, delta, initial, expiration.ToTtl());
        }

        /// <summary>
        /// Decrements the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<ulong> Decrement(string key)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial)
        {
            //infinite - there is also a 'special' value -1: 'don't create if missing'
            const uint expiration = 0;

            return Decrement(key, delta, initial, expiration);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds. See <see cref="IBucket"/> doc section on TTL.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration)
        {
            var operation = new Decrement(key, initial, delta, expiration, null, _transcoder,
                _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return Decrement(key, delta, initial, expiration.ToTtl());
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<string> Append(string key, string value)
        {
            var operation = new Append<string>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Append(string key, byte[] value)
        {
            var operation = new Append<byte[]>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }


        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<string> Prepend(string key, string value)
        {
            var operation = new Prepend<string>(key, value, null,  _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Prepend(string key, byte[] value)
        {
            var operation = new Prepend<byte[]>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetry(operation);
        }

        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            CheckDisposed();
            var operation = new Get<T>(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        public Task<IViewResult<T>> QueryAsync<T>(IViewQueryable query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewResult<T> Query<T>(IViewQueryable query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public N1QL.IQueryResult<T> Query<T>(string query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(bool development)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(string designDoc, string view)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewQuery CreateQuery(string designdoc, string view, bool development)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Creates a <see cref="MemcachedBucketManager" /> instance for managing buckets.
        /// </summary>
        /// <param name="username">The administrators username</param>
        /// <param name="password">The administrators username</param>
        /// <returns>
        /// A <see cref="MemcachedBucketManager" /> instance.
        /// </returns>
        public IBucketManager CreateManager(string username, string password)
        {
            return new MemcachedBucketManager(this,
               _configInfo.ClientConfig,
               new JsonDataMapper(_configInfo.ClientConfig),
               username,
               password);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await UpsertAsync<T>(document.Id, document.Content, document.Cas,
                    document.Expiry.ToTtl()).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(UpsertAsync(doc)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration)
        {
            return UpsertAsync(key, value, 0, expiration);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration)
        {
            return UpsertAsync(key, value, 0, expiration.ToTtl());
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas)
        {
            return UpsertAsync(key, value, cas, 0);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync<T>(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return UpsertAsync(key, value, cas, expiration.ToTtl());
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await ReplaceAsync<T>(document.Id, document.Content, document.Cas, document.Expiry.ToTtl()).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(ReplaceAsync(doc)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// A <see cref="Task{IDocumentResult}" /> list.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value)
        {
            CheckDisposed();
            var operation = new Replace<T>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration)
        {
            return ReplaceAsync(key, value, 0, expiration);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration)
        {
            return ReplaceAsync(key, value, 0, expiration.ToTtl());
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas)
        {
            return ReplaceAsync(key, value, cas, (uint)0);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration)
        {
            CheckDisposed();
            var operation = new Replace<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync<T>(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            return ReplaceAsync(key, value, cas, expiration.ToTtl());
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");

        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await InsertAsync<T>(document.Id, document.Content, document.Expiry.ToTtl()).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, _operationLifespanTimeout)
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration)
        {
            return InsertAsync(key, value, expiration.ToTtl());
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> to remove from the database.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document)
        {
            CheckDisposed();
            var operation = new Delete(document.Id, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            CheckDisposed();
            var operation = new Delete(document.Id, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a list of <see cref="IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <returns>
        /// A list of <see cref="Task{IOperationResult}" /> objects representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents)
        {
            var tasks = new List<Task<IOperationResult>>();
            documents.ForEach(doc => tasks.Add(RemoveAsync(doc)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Removes a list of <see cref="IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// A list of <see cref="Task{IOperationResult}" /> objects representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a list of <see cref="IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// A list of <see cref="Task{IOperationResult}" /> objects representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }


        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, persistTo);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, _operationLifespanTimeout)
            {
                Cas = cas
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, persistTo);
        }

        /// <summary>
        ///     Gets a document by it's given id asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>
        ///  An <see cref="IDocumentResult{T}" /> object containing the document if it's found and any other operation specific info.
        /// </returns>
        public async Task<IDocumentResult<T>> GetDocumentAsync<T>(string id)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await GetAsync<T>(id).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetWithLock{T}" /> as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="System.NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult> UnlockAsync(string key, ulong cas)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Increments the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created.
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>/// <returns></returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key)
        {
            return IncrementAsync(key, 1);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta)
        {
            return IncrementAsync(key, delta, 1);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial)
        {
            return IncrementAsync(key, delta, initial, 0);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration)
        {
            CheckDisposed();
            var operation = new Increment(key, initial, delta, expiration, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return IncrementAsync(key, delta, initial, expiration.ToTtl());
        }

        /// <summary>
        /// Decrements the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> DecrementAsync(string key)
        {
            return DecrementAsync(key, 1);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta)
        {
            return DecrementAsync(key, delta, 1);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial)
        {
            return DecrementAsync(key, delta, initial, 0);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration)
        {
            CheckDisposed();
            var operation = new Decrement(key, initial, delta, expiration, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration)
        {
            return DecrementAsync(key, delta, initial, expiration.ToTtl());
        }

        /// <summary>
        /// Appends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append to.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<string>> AppendAsync(string key, string value)
        {
            CheckDisposed();
            var operation = new Append<string>(key, value, null, _transcoder, _operationLifespanTimeout);
            var result = _requestExecuter.SendWithRetryAsync(operation);
            return result;
        }

        /// <summary>
        /// Appends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append to.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value)
        {
            CheckDisposed();
            var operation = new Append<byte[]>(key, value, null, _transcoder, _operationLifespanTimeout);
            var result = _requestExecuter.SendWithRetryAsync(operation);
            return result;
        }

        /// <summary>
        /// Prepends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend to.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<string>> PrependAsync(string key, string value)
        {
            CheckDisposed();
            var operation = new Prepend<string>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Prepends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend to.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value)
        {
            CheckDisposed();
            var operation = new Prepend<byte[]>(key, value, null, _transcoder, _operationLifespanTimeout);
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Increments the reference counter for this <see cref="IBucket"/> instance.
        /// </summary>
        /// <returns>The current count of all <see cref="IBucket"/> references.</returns>
        public int AddRef()
        {
            lock (RefCounts)
            {
                var refCount = RefCounts.GetOrCreateValue(this);
                return Interlocked.Increment(ref refCount.Count);
            }
        }

        /// <summary>
        /// Decrements the reference counter and calls <see cref="IDisposable.Dispose"/> if the count is zero.
        /// </summary>
        /// <returns></returns>
        public int Release()
        {
            lock (RefCounts)
            {
                var refCount = RefCounts.GetOrCreateValue(this);
                if (refCount.Count > 0)
                {
                    Interlocked.Decrement(ref refCount.Count);
                    if (refCount.Count != 0) return refCount.Count;
                    RefCounts.Remove(this);
                    Dispose(true);
                }
                else
                {
                    Dispose(true);
                }
                return refCount.Count;
            }
        }

        void CheckDisposed()
        {
            if (_disposed)
            {
                var message = string.Format("This bucket [{0}] has been disposed! Performing operations on a disposed bucket is not supported!", Name);
                throw new ObjectDisposedException(message);
            }
        }

        /// <summary>
        /// Closes this <see cref="MemcachedBucket"/> instance, shutting down and releasing all resources,
        /// removing it from it's <see cref="ClusterController"/> instance.
        /// </summary>
        public void Dispose()
        {
            Release();
        }

        /// <summary>
        /// Closes this <see cref="MemcachedBucket"/> instance, shutting down and releasing all resources,
        /// removing it from it's <see cref="ClusterController"/> instance.
        /// </summary>
        /// <param name="disposing">If true suppresses finalization.</param>
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _clusterController.DestroyBucket(this);
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _disposed = true;
            }
        }

#if DEBUG
        /// <summary>
        /// Finalizer for this <see cref="MemcachedBucket"/> instance if not shutdown and disposed gracefully.
        /// </summary>
        ~MemcachedBucket()
        {
            Dispose(false);
        }
#endif

        #region sub document api

        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        #endregion

        #region FTS (Not Supported)

        public ISearchQueryResult Query(SearchQuery query1)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<ISearchQueryResult> QueryAsync(SearchQuery query1)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        #endregion

        #region  Data Structures (Not Supported)

        public IResult<TContent> MapGet<TContent>(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult MapRemove(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<int> MapSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult MapAdd(string key, string mapkey, string value, bool createMap)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<TContent> ListGet<TContent>(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult ListAppend(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult ListPrepend(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult ListRemove(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult ListSet(string key, int index, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<int> ListSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult SetAdd(string key, string value, bool createSet)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<bool> SetContains(string key, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<int> SetSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult SetRemove<T>(string key, T value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult QueuePush<T>(string key, T value, bool createQueue)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<T> QueuePop<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IResult<int> QueueSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> MapRemoveAsync(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<int>> MapSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> ListAppendAsync(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> ListPrependAsync(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> ListRemoveAsync(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> ListSetAsync(string key, int index, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<int>> ListSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> SetAddAsync(string key, string value, bool createSet)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<bool>> SetContainsAsync(string key, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<int>> SetSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> SetRemoveAsync<T>(string key, T value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<T>> QueuePopAsync<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IResult<int>> QueueSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IAnalyticsResult<T> Query<T>(IAnalyticsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion
