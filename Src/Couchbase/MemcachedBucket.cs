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
 using Couchbase.Core.Version;
 using Couchbase.IO.Converters;
 using Couchbase.IO.Operations;
 using Couchbase.Management;
 using Couchbase.N1QL;
 using Couchbase.Search;
 using Couchbase.Views;
 using Couchbase.Utils;
using Couchbase.Core.Monitoring;

namespace Couchbase
{
    /// <summary>
    /// Represents an in-memory bucket for storing Key/Value pairs. Most often used as a distributed cache.
    /// </summary>
    /// <seealso cref="Couchbase.Core.IBucket" />
    /// <seealso cref="Couchbase.Configuration.Server.Providers.IConfigObserver" />
    /// <seealso cref="Couchbase.IRefCountable" />
    public sealed class MemcachedBucket : IBucket, IConfigObserver, IRefCountable
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
        private readonly IAuthenticator _authenticator;

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

        IConfigInfo IConfigObserver.ConfigInfo
        {
            get { return _configInfo; }
        }

        internal MemcachedBucket(IClusterController clusterController, string bucketName, IByteConverter converter,
            ITypeTranscoder transcoder, IAuthenticator authenticator)
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

            //the global timeout for all operations unless an overload with timeout is called
            GlobalTimeout = new TimeSpan(0, 0, 0, (int)_operationLifespanTimeout);

            _authenticator = authenticator;
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
        /// The default or globally set operation lifetime.
        /// </summary>
        private TimeSpan GlobalTimeout { get; set; }

        /// <summary>
        /// Creates a <see cref="IBucketManager" /> instance for managing buckets using the <see cref="IAuthenticator" /> for authentication.
        /// </summary>
        /// <returns>
        /// A <see cref="IBucketManager" /> instance.
        /// </returns>
        /// <exception cref="AuthenticationException">
        /// No credentials found.
        /// </exception>
        public IBucketManager CreateManager()
        {
            if (_authenticator == null)
            {
                throw new AuthenticationException("No credentials found.");
            }

            var clusterCreds = _authenticator.GetCredentials(AuthContext.ClusterMgmt).FirstOrDefault();
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
        public bool SupportsEnhancedDurability
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the cluster supports an error map that can
        /// be used to return custom error information.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports KV error map; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsKvErrorMap
        {
            get { return false; }
        }

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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks for the existance of a given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// True if the key exists.
        /// </returns>
        public bool Exists(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>A <see cref="Task{boolean}"/> object representing the asynchronous operation.</returns>
        public Task<bool> ExistsAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<bool> ExistsAsync(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied asynchronously.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:System.Threading.Tasks.Task`1" /> value indicating if the durability requirement were or were not met.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public ObserveResponse Observe(string key, ulong cas, bool remove, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IO.Operations.ObserveResponse" /> value indicating if the durability requirement were or were not met.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied asynchronously.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An <see cref="T:System.Threading.Tasks.Task`1" /> value indicating if the durability requirement were or were not met.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="T:System.Threading.Tasks.ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="T:System.Threading.CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout)
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
                        var result = Remove(key, timeout);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        public IOperationResult Touch(string key, TimeSpan expiration)
        {
            return Touch(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        public IOperationResult Touch(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var touch = new Touch(key, null, _transcoder, timeout.GetMilliseconds())
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
            return TouchAsync(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:System.Threading.Tasks.Task`1" />object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> TouchAsync(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var touch = new Touch(key, null, _transcoder, timeout.GetMilliseconds())
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
            return Upsert(document, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, TimeSpan timeout)
        {
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), timeout);
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Upserts a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Upserts a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Upserts a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
            return Upsert(key, value, TimeSpan.Zero);
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
            return Upsert(key, value, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return UpsertAsync(key, value, 0, expiration, timeout);
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
            return Upsert(key, value, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return UpsertAsync(key, value, 0, expiration.ToTtl(), timeout);
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
            var operation = new Set<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds())
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
            return Upsert(key, value, cas, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Cas = cas,
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return Upsert(key, value, cas, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Upserts the specified key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiration">The expiration.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");

        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
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
            return Upsert<T>(items, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="T:System.Collections.Generic.IDictionary`2" /> of items to be stored in Couchbase.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IDictionary`2" /> of <see cref="T:Couchbase.IOperationResult" /> which for which each is the result of the individual operation.
        /// </returns>
        /// <remarks>
        /// An item is <see cref="T:System.Collections.Generic.KeyValuePair`2" /> where K is a <see cref="T:System.String" /> and V is the <see cref="T:System.Type" />of the value use wish to store.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, TimeSpan timeout)
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
                        const UInt32 expiration = 0;
                        var result = Upsert(key, value, expiration, timeout);
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
        /// <param name="items">A <see cref="T:System.Collections.Generic.IDictionary`2" /> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IDictionary`2" /> of <see cref="T:Couchbase.IOperationResult" /> which for which each is the result of the individual operation.
        /// </returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <remarks>
        /// An item is <see cref="T:System.Collections.Generic.KeyValuePair`2" /> where K is a <see cref="T:System.String" /> and V is the <see cref="T:System.Type" />of the value use wish to store.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, TimeSpan timeout)
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
                        const UInt32 expiration = 0;
                        var result = Upsert(key, value, expiration, timeout);
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
            return Upsert(items, options, rangeSize, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="T:System.Collections.Generic.IDictionary`2" /> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IDictionary`2" /> of <see cref="T:Couchbase.IOperationResult" /> which for which each is the result of the individual operation.
        /// </returns>
        /// <remarks>
        /// An item is <see cref="T:System.Collections.Generic.KeyValuePair`2" /> where K is a <see cref="T:System.String" /> and V is the <see cref="T:System.Type" />of the value use wish to store.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize, TimeSpan timeout)
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
                        const UInt32 expiration = 0;
                        var result = Upsert(key, value, expiration, timeout);
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
            return Replace(document, GlobalTimeout);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, TimeSpan timeout)
        {
            var result = Replace(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), timeout);
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, 0, expiration.ToTtl(), timeout);
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
            var operation = new Replace<T>(key, value, cas, null, _transcoder, GlobalTimeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
            var operation = new Replace<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds());
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
            return Replace(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, 0, expiration, timeout);
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

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return Replace(key, value, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo,
            PersistTo persistTo)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
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
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
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
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
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

        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
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

        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
            return Replace(key, value, cas, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration,
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync<T>(operation);
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
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return Replace(key, value, cas, expiration.ToTtl(), timeout);
        }

        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types."); ;
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document)
        {
            return Insert(document, GlobalTimeout);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, TimeSpan timeout)
        {
            var result = Insert(document.Id, document.Content, document.Expiry.ToTtl(), timeout);
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
            return InsertAsync(documents, GlobalTimeout);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(InsertAsync(doc, timeout)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException"></exception>
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
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
        /// <exception cref="System.NotSupportedException"></exception>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
            var operation = new Add<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds());
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
            return Insert(key, value, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return Insert(key, value, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
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
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IOperationResult Remove<T>(IDocument<T> document, TimeSpan timeout)
        {
            return Remove(document.Id, timeout);
        }

        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key)
        {
            return Remove(key, GlobalTimeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, TimeSpan timeout)
        {
            var operation = new Delete(key, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Asynchronously removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas)
        {
            return Remove(key, cas, GlobalTimeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, ulong cas, TimeSpan timeout)
        {
            var operation = new Delete(key, null, _transcoder, timeout.GetMilliseconds())
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
            return Remove(keys, GlobalTimeout);
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, TimeSpan timeout)
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
                        var result = Remove(key, timeout);
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
            return Remove(keys, options, GlobalTimeout);
        }

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="T:System.Threading.Tasks.ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="T:System.Threading.CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, TimeSpan timeout)
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
            return Remove(keys, options, rangeSize, GlobalTimeout);
        }

        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Asynchronously removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key)
        {
            return RemoveAsync(key, GlobalTimeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:System.Threading.Tasks.Task`1" />object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            var result = GetAndTouchAsync<T>(key, expiration, timeout);
            tcs.SetResult(new DocumentResult<T>(result.Result));
            return tcs.Task;
        }

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        public IDocumentResult<T> GetDocument<T>(string id)
        {
            return GetDocument<T>(id, GlobalTimeout);
        }

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IDocumentResult`1" /> object containing the document if it's found and any other operation specific info.
        /// </returns>
        public IDocumentResult<T> GetDocument<T>(string id, TimeSpan timeout)
        {
            var result = Get<T>(id, timeout);
            return new DocumentResult<T>(result);
        }

        /// <summary>
        /// Gets a document by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> GetDocumentAsync<T>(string id, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await GetAsync<T>(id, timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
            return GetDocumentsAsync<T>(ids, GlobalTimeout);
        }

        /// <summary>
        /// Gets a list of documents by their given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="ids">The documents primary keys.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> array representing the asynchronous operation results.
        /// </returns>
        public Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            ids.ToList().ForEach(id => tasks.Add(GetDocumentAsync<T>(id, timeout)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets a document using a replica by it's given id. Unsupported for Memcached buckets.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="IDocumentResult{T}"/></returns>

        public IDocumentResult<T> GetDocumentFromReplica<T>(string id)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IDocumentResult<T> GetDocumentFromReplica<T>(string id, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document using a replica by it's given id as an asynchronous operation. Unsupported for Memcached buckets.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="Task{IDocumentResult{T}}"/> object representing the asynchronous operation.</returns>
        public Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a value for a given key from a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            return Get<T>(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets value for a given key
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="key">The key to use as a lookup.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Get<T>(string key, TimeSpan timeout)
        {
            var operation = new Get<T>(key, null, _transcoder, timeout.GetMilliseconds());
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
            return GetAndTouch<T>(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the key's value.
        /// </returns>
        public IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var operation = new GetT<T>(key, null, _transcoder, timeout.GetMilliseconds())
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
            return GetAndTouchAsync<T>(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:System.Threading.Tasks.Task`1" />object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var operation = new GetT<T>(key, null, _transcoder, timeout.GetMilliseconds())
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
            return GetAndTouchDocument<T>(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IDocumentResult`1" /> with the key's document.
        /// </returns>
        public IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var result = GetAndTouch<T>(key, expiration, timeout);
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
            return GetAndTouchDocumentAsync<T>(key, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> GetAsync<T>(string key, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Get<T>(key, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        public IOperationResult<T> GetFromReplica<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> GetFromReplica<T>(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key, TimeSpan timeout)
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
            return Get<T>(keys, GlobalTimeout);
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, TimeSpan timeout)
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
                        var result = Get<T>(key, timeout);
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
            return Get<T>(keys, options, GlobalTimeout);
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="T:System.Threading.Tasks.ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="T:System.Threading.CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, TimeSpan timeout)
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
                        var result = Get<T>(key, timeout);
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
            return Get<T>(keys, options, rangeSize, GlobalTimeout);
        }

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="T:System.Threading.Tasks.ParallelOptions" /> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.Dictionary`2" /> of the keys sent and the <see cref="T:Couchbase.IOperationResult`1" /> result.
        /// </returns>
        /// <remarks>
        /// Use the <see cref="T:System.Threading.Tasks.ParallelOptions" /> parameter to control the level of parallelism to use and/or to associate a <see cref="T:System.Threading.CancellationToken" /> with the operation.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout)
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
                        var result = Get<T>(key, timeout);
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetWithLock<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> with the value.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="T:System.Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="M:Couchbase.Core.IBucket.GetAndLock``1(System.String,System.UInt32)" />.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Unlock(string key, ulong cas)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="M:Couchbase.Core.IBucket.GetAndLock``1(System.String,System.UInt32)" />.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Unlock(string key, ulong cas, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="M:Couchbase.Core.IBucket.GetAndLockAsync``1(System.String,System.UInt32)" /> as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult> UnlockAsync(string key, ulong cas, TimeSpan timeout)
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
            return Increment(key, GlobalTimeout);
        }

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        public IOperationResult<ulong> Increment(string key, TimeSpan timeout)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration, timeout);
        }

        /// <summary>
        /// Increments the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created.
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, TimeSpan timeout)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return IncrementAsync(key, delta, initial, expiration, timeout);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Increment(string key, ulong delta, TimeSpan timeout)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Increment(key, delta, initial, expiration, timeout);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </remarks>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, TimeSpan timeout)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return IncrementAsync(key, delta, initial, expiration, timeout);
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
            return Increment(key, delta, initial, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            var operation = new Increment(key, initial, delta, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// &gt;
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Increment(key, initial, delta, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return Increment(key, delta, initial, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.
        /// </remarks>
        public Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return IncrementAsync(key, delta, initial, expiration.ToTtl(), timeout);
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
        /// Decrements the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Decrement(string key, TimeSpan timeout)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration, timeout);
        }

        /// <summary>
        /// Decrements the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </remarks>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, TimeSpan timeout)
        {
            const ulong initial = 1;
            const ulong delta = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return DecrementAsync(key, delta, initial, expiration, timeout);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, TimeSpan timeout)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return Decrement(key, delta, initial, expiration, timeout);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </remarks>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, TimeSpan timeout)
        {
            const ulong initial = 1;
            const uint expiration = 0; //infinite - there is also a 'special' value -1: 'don't create if missing'

            return DecrementAsync(key, delta, initial, expiration, timeout);
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
            return Decrement(key, delta, initial, expiration, GlobalTimeout);
        }

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            var operation = new Decrement(key, initial, delta, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };

            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Decrement(key, initial, delta, null, _transcoder, timeout.GetMilliseconds())
            {
                Expires = expiration
            };

            return _requestExecuter.SendWithRetryAsync(operation);
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
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return Decrement(key, delta, initial, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.
        /// </remarks>
        public Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout)
        {
            return DecrementAsync(key, delta, initial, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<string> Append(string key, string value)
        {
            return Append(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status of the operation.
        /// </returns>
        public IOperationResult<string> Append(string key, string value, TimeSpan timeout)
        {
            var operation = new Append<string>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<string>> AppendAsync(string key, string value, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Append<string>(key, value, null, _transcoder, timeout.GetMilliseconds());
            var result = _requestExecuter.SendWithRetryAsync(operation);
            return result;
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Append(string key, byte[] value)
        {
            return Append(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status of the operation.
        /// </returns>
        public IOperationResult<byte[]> Append(string key, byte[] value, TimeSpan timeout)
        {
            var operation = new Append<byte[]>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Append<byte[]>(key, value, null, _transcoder, timeout.GetMilliseconds());
            var result = _requestExecuter.SendWithRetryAsync(operation);
            return result;
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<string> Prepend(string key, string value)
        {
            return Prepend(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status of the operation.
        /// </returns>
        public IOperationResult<string> Prepend(string key, string value, TimeSpan timeout)
        {
            var operation = new Prepend<string>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<string>> PrependAsync(string key, string value, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Prepend<string>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Prepend(string key, byte[] value)
        {
            return Prepend(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> with the status of the operation.
        /// </returns>
        public IOperationResult<byte[]> Prepend(string key, byte[] value, TimeSpan timeout)
        {
            var operation = new Prepend<byte[]>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            return GetAsync<T>(key, GlobalTimeout);
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        public Task<IViewResult<T>> QueryAsync<T>(IViewQueryable query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Prepend<byte[]>(key, value, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="T:Couchbase.Views.IViewQuery" /> used to generate the results.</param>
        /// <returns>
        /// An instance of an object that implements the <see cref="T:Couchbase.Views.IViewResult{T}" /> Type with the results of the query.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Use one of the IBucket.CreateQuery overloads to generate the query.
        /// </remarks>
        public IViewResult<T> Query<T>(IViewQueryable query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>
        /// An instance of an object that implements the <see cref="T:Couchbase.N1QL.IQueryResult`1" /> interface; the results of the query.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public N1QL.IQueryResult<T> Query<T>(string query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Asynchronously executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>
        /// An awaitable <see cref="T:System.Threading.Tasks.Task`1" /> with the T a <see cref="T:Couchbase.N1QL.IQueryResult`1" /> instance.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Note this implementation is uncommitted/experimental and subject to change in future release!
        /// </remarks>
        public Task<IQueryResult<T>> QueryAsync<T>(string query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Executes a N1QL statement or prepared statement via a <see cref="T:Couchbase.N1QL.IQueryRequest" /> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="T:Couchbase.N1QL.IQueryRequest" /> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>
        /// An instance of an object that implements the <see cref="T:Couchbase.N1QL.IQueryResult`1" /> interface; the results of the query.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="T:Couchbase.N1QL.IQueryRequest" /> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="T:Couchbase.N1QL.IQueryRequest" /> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>
        /// An instance of an object that implements the <see cref="T:Couchbase.N1QL.IQueryResult`1" /> interface; the results of the query.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="T:Couchbase.N1QL.IQueryRequest" /> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="T:Couchbase.N1QL.IQueryRequest" /> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>
        /// An instance of an object that implements the <see cref="T:Couchbase.N1QL.IQueryResult`1" /> interface; the results of the query.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Creates the query.
        /// </summary>
        /// <param name="development">if set to <c>true</c> [development].</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IViewQuery CreateQuery(bool development)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="T:Couchbase.Views.IViewQuery" />, which targets a given bucket, design document and a published view.
        /// </summary>
        /// <param name="designDoc"></param>
        /// <param name="view"></param>
        /// <returns>
        /// An <see cref="T:Couchbase.Views.IViewQuery" /> which can have more filters and options applied to it.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IViewQuery CreateQuery(string designDoc, string view)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="T:Couchbase.Views.IViewQuery" />, which targets a given bucket and design document.
        /// </summary>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <param name="view">The View to query.</param>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Views.ViewQuery" /> which can have more filters and options applied to it.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IViewQuery CreateQuery(string designdoc, string view, bool development)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await UpsertAsync(document.Id, document.Content, document.Cas,
                    document.Expiry.ToTtl(), timeout).ContinueOnAnyContext();

                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return UpsertAsync(key, value, cas, expiration.ToTtl(), timeout);
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await ReplaceAsync<T>(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, cas, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo)
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await InsertAsync(document.Id, document.Content, document.Expiry.ToTtl(), timeout)
                    .ContinueOnAnyContext();

                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return InsertAsync(key, value, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(document.Id, null, _transcoder, timeout.GetMilliseconds());
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, timeout.GetMilliseconds())
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove(string key, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types."); ;
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
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
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document)
        {
            return UpsertAsync(document, GlobalTimeout);
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

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
            return UpsertAsync(documents, GlobalTimeout);
        }

        /// <summary>
        /// Upserts a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(UpsertAsync(doc, timeout)));
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
            var operation = new Set<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds());
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
            return UpsertAsync(key, value, cas, expiration, GlobalTimeout);
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

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration,
            ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document)
        {
            return ReplaceAsync(document, GlobalTimeout);
        }

        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
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
            return ReplaceAsync(documents, GlobalTimeout);
        }

        /// <summary>
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(ReplaceAsync(doc, timeout)));
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

        /// <summary>
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
            var operation = new Replace<T>(key, value, null, _transcoder, GlobalTimeout.GetMilliseconds());
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
            return ReplaceAsync(key, value, cas, expiration, GlobalTimeout);
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

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document)
        {
            return InsertAsync(document, GlobalTimeout);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
            return InsertAsync(key, value, expiration, GlobalTimeout);
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

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo,
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo,
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
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
            return RemoveAsync(document, GlobalTimeout);
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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
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
            return RemoveAsync(documents, GlobalTimeout);
        }

        /// <summary>
        /// Removes a list of <see cref="T:Couchbase.IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A list of <see cref="T:System.Threading.Tasks.Task`1" /> objects representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
        {
            var tasks = new List<Task<IOperationResult>>();
            documents.ForEach(doc => tasks.Add(RemoveAsync(doc, timeout)));
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
        /// Removes a list of <see cref="T:Couchbase.IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A list of <see cref="T:System.Threading.Tasks.Task`1" /> objects representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
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
            return RemoveAsync(key, cas, GlobalTimeout);
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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
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
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        ///     Gets a document by it's given id asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>
        ///  An <see cref="IDocumentResult{T}" /> object containing the document if it's found and any other operation specific info.
        /// </returns>
        public Task<IDocumentResult<T>> GetDocumentAsync<T>(string id)
        {
            return GetDocumentAsync<T>(id, GlobalTimeout);
        }

        /// <summary>
        /// Gets a value for key and checks it's replicas as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
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
            return IncrementAsync(key, delta, initial, expiration, GlobalTimeout);
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
            return DecrementAsync(key, delta, initial, expiration, GlobalTimeout);
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
            return AppendAsync(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Appends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append to.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value)
        {
            return AppendAsync(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Prepends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend to.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<string>> PrependAsync(string key, string value)
        {
            return PrependAsync(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Prepends a value to a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend to.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value)
        {
            return PrependAsync(key, value, GlobalTimeout);
        }

        /// <summary>
        /// Gets the cluster version using the configured bucket or cluster credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        public ClusterVersion? GetClusterVersion()
        {
            return ClusterVersionProvider.Instance.GetVersion(this);
        }

        /// <summary>
        /// Gets the cluster version using the configured bucket or cluster credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        public Task<ClusterVersion?> GetClusterVersionAsync()
        {
            return ClusterVersionProvider.Instance.GetVersionAsync(this);
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

        /// <summary>
        /// Mutates the in.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Mutates the in.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Lookups the in.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Lookups the in.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        #endregion

        #region FTS (Not Supported)

        /// <summary>
        /// Queries the specified query1.
        /// </summary>
        /// <param name="query1">The query1.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public ISearchQueryResult Query(SearchQuery query1)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Queries the asynchronous.
        /// </summary>
        /// <param name="query1">The query1.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<ISearchQueryResult> QueryAsync(SearchQuery query1)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        #endregion

        #region  Data Structures (Not Supported)

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<TContent> MapGet<TContent>(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<TContent> MapGet<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult MapRemove(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult MapRemove(string key, string mapkey, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> MapSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> MapSize(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult MapAdd(string key, string mapkey, string value, bool createMap)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult MapAdd(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<TContent> ListGet<TContent>(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<TContent> ListGet<TContent>(string key, int index, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListAppend(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListAppend(string key, object value, bool createList, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListPrepend(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListPrepend(string key, object value, bool createList, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListRemove(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListRemove(string key, int index, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListSet(string key, int index, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult ListSet(string key, int index, string value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> ListSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> ListSize(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult SetAdd(string key, string value, bool createSet)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult SetAdd(string key, string value, bool createSet, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<bool> SetContains(string key, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<bool> SetContains(string key, string value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> SetSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> SetSize(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult SetRemove<T>(string key, T value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult SetRemove<T>(string key, T value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult QueuePush<T>(string key, T value, bool createQueue)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult QueuePush<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<T> QueuePop<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<T> QueuePop<T>(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> QueueSize(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IResult<int> QueueSize(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> MapRemoveAsync(string key, string mapkey)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> MapRemoveAsync(string key, string mapkey, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> MapSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> MapSizeAsync(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The value as <see cref="T:Couchbase.IResult`1" />
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListAppendAsync(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListAppendAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListPrependAsync(string key, object value, bool createList)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListPrependAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListRemoveAsync(string key, int index)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListRemoveAsync(string key, int index, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListSetAsync(string key, int index, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> ListSetAsync(string key, int index, string value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> ListSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> ListSizeAsync(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> SetAddAsync(string key, string value, bool createSet)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> SetAddAsync(string key, string value, bool createSet, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<bool>> SetContainsAsync(string key, string value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<bool>> SetContainsAsync(string key, string value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> SetSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> SetSizeAsync(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> SetRemoveAsync<T>(string key, T value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> SetRemoveAsync<T>(string key, T value, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<T>> QueuePopAsync<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<T>> QueuePopAsync<T>(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IResult<int>> QueueSizeAsync(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        /// <!-- Badly formed XML comment ignored for member "M:Couchbase.Core.IBucket.QueueSizeAsync(System.String,System.TimeSpan)" -->
        public Task<IResult<int>> QueueSizeAsync(string key, TimeSpan timeout)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Queries the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public IAnalyticsResult<T> Query<T>(IAnalyticsRequest request)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Queries the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Queries the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">This method is only supported on Couchbase Bucket (persistent) types.</exception>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request, CancellationToken token)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <inheritdoc />
        public string ExportDeferredAnalyticsQueryHandle<T>(IAnalyticsDeferredResultHandle<T> handle)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <inheritdoc />
        public IAnalyticsDeferredResultHandle<T> ImportDeferredAnalyticsQueryHandle<T>(string encodedHandle)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Pings the specified services.
        /// </summary>
        /// <param name="services">The services to ping. Default is all services.</param>
        /// <returns>
        /// An <see cref="IPingReport"/> for the requested services.
        /// </returns>
        public IPingReport Ping(params ServiceType[] services)
        {
            return Ping(Guid.NewGuid().ToString(), services);
        }

        /// <summary>
        /// Pings the specified services.
        /// </summary>
        /// <param name="reportId">The report identifier.</param>
        /// <param name="services">The services to ping. Default is all services.</param>
        /// <returns>
        /// An <see cref="IPingReport"/> for the requested services.
        /// </returns>
        public IPingReport Ping(string reportId, params ServiceType[] services)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                throw new ArgumentException(nameof(reportId));
            }

            return DiagnosticsReportProvider.CreatePingReport(reportId, _configInfo, services);
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
