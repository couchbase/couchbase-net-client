 using System;
 using System.Collections.Concurrent;
 using System.Collections.Generic;
 using System.Linq;
 using System.Runtime.CompilerServices;
 using System.Threading;
 using System.Threading.Tasks;
 using Common.Logging;
 using Couchbase.Annotations;
 using Couchbase.Configuration;
 using Couchbase.Configuration.Server.Providers;
 using Couchbase.Core;
 using Couchbase.Core.Transcoders;
 using Couchbase.IO;
 using Couchbase.IO.Converters;
 using Couchbase.IO.Operations;
 using Couchbase.Management;
 using Couchbase.N1QL;
 using Couchbase.Views;
 using Couchbase.Utils;

namespace Couchbase
{
    /// <summary>
    /// Represents an in-memory bucket for storing Key/Value pairs. Most often used as a distributed cache.
    /// </summary>
    public class MemcachedBucket : IBucket, IConfigObserver, IRefCountable
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterController _clusterManager;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private static readonly object SyncObj = new object();
        private readonly IByteConverter _converter;
        private readonly ITypeTranscoder _transcoder;

        /// <summary>
        /// Used for reference counting instances so that <see cref="IDisposable.Dispose"/> is only called by the last instance.
        /// </summary>
        private static readonly ConditionalWeakTable<IDisposable, RefCount> RefCounts = new ConditionalWeakTable<IDisposable, RefCount>();

        [UsedImplicitly]
        private sealed class RefCount
        {
            public int Count;
        }

        internal MemcachedBucket(IClusterController clusterManager, string bucketName, IByteConverter converter, ITypeTranscoder transcoder)
        {
            _clusterManager = clusterManager;
            _converter = converter;
            _transcoder = transcoder;
            Name = bucketName;
        }

        /// <summary>
        /// The Bucket's name. You can view this from the Couchbase Management Console.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns type of the bucket. In this implementation the value is constant: Memcached.
        /// </summary>
        public Couchbase.Core.Buckets.BucketTypeEnum BucketType
        {
            get
            {
                return Couchbase.Core.Buckets.BucketTypeEnum.Memcached;
            }
        }

        /// <summary>
        /// Returns true if bucket is using SSL encryption between the client and the server.
        /// </summary>
        public bool IsSecure
        {
            get
            {
                var server = _configInfo.GetServer();
                return server.IsSecure;
            }
        }

        /// <summary>
        /// Called when a configuration update has occurred from the server.
        /// </summary>
        /// <param name="configInfo">The new configuration</param>
        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Log.Info(m => m("Config updated old/new: {0}, {1}",
               _configInfo != null ? _configInfo.BucketConfig.Rev : 0, configInfo.BucketConfig.Rev));
            Interlocked.Exchange(ref _configInfo, configInfo);
        }

        IServer GetServer(string key)
        {
            var keyMapper = _configInfo.GetKeyMapper();
            var bucket = keyMapper.MapKey(key);
            return bucket.LocatePrimary();
        }

        private IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            CheckDisposed();
            IOperationResult<T> operationResult = new OperationResult<T>{Success = false};
            do
            {
                var server = GetServer(operation.Key);
                if (server == null)
                {
                    continue;
                }
                operationResult = server.Send(operation);
                if (operationResult.Success)
                {
                    Log.Debug(m => m("Operation succeeded {0} for key {1}", operation.Attempts, operation.Key));
                    break;
                }
                if (CanRetryOperation(operationResult, operation, server))
                {
                    Log.Debug(m => m("Operation retry {0} for key {1}. Reason: {2}", operation.Attempts,
                        operation.Key, operationResult.Message));
                }
                else
                {
                    Log.Debug(m => m("Operation doesn't support retries for key {0}", operation.Key));
                    break;
                }
            } while (operation.Attempts++ < operation.MaxRetries && !operationResult.Success);

            if (!operationResult.Success)
            {
                Log.Debug(
                    m =>
                        m("Operation for key {0} failed after {1} retries. Reason: {2}", operation.Key,
                            operation.Attempts, operationResult.Message));
            }
            return operationResult;
        }

        static bool OperationSupportsRetries<T>(IOperation<T>  operation)
        {
            var supportsRetry = false;
            switch (operation.OperationCode)
            {
                case OperationCode.Get:
                case OperationCode.Add:
                    supportsRetry = true;
                    break;
                case OperationCode.Set:
                case OperationCode.Replace:
                case OperationCode.Delete:
                case OperationCode.Append:
                case OperationCode.Prepend:
                    supportsRetry = operation.Cas > 0;
                    break;
                case OperationCode.Increment:
                case OperationCode.Decrement:
                case OperationCode.GAT:
                case OperationCode.Touch:
                    break;
            }
            return supportsRetry;
        }

        bool CanRetryOperation<T>(IOperationResult<T> operationResult, IOperation<T> operation, IServer server)
        {
            var supportsRetry = OperationSupportsRetries(operation);
            var retry = false;
            switch (operationResult.Status)
            {
                case ResponseStatus.Success:
                case ResponseStatus.KeyNotFound:
                case ResponseStatus.KeyExists:
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    break;
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.InvalidRange:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.NotSupported:
                case ResponseStatus.InternalError:
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    break;
                case ResponseStatus.ClientFailure:
                    retry = supportsRetry;
                    break;
            }
            return retry;
        }

        public ObserveResponse Observe(string key, ulong cas, bool remove, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document)
        {
            var result = Upsert(document.Id, document.Content);
            return new DocumentResult<T>(result, document.Id);
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
            var operation = new Set<T>(key, value, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            var operation = new Set<T>(key, value, null, _converter, _transcoder)
            {
                Expires = expiration
            };
            return SendWithRetry(operation);
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
            var operation = new Set<T>(key, value, null, _converter, _transcoder)
            {
                Cas = cas
            };
            return SendWithRetry(operation);
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
            var operation = new Set<T>(key, value, null, _converter, _transcoder)
            {
                Cas = cas,
                Expires = expiration
            };
            return SendWithRetry(operation);
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

        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");

        }

        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
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
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize)
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
            var result = Replace(document.Id, document.Content);
            return new DocumentResult<T>(result, document.Id);
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
            var operation = new Add<T>(key, value, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            var operation = new Replace<T>(key, value, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            var operation = new Replace<T>(key, value, null, _converter, _transcoder)
            {
                Expires = expiration
            };
            return SendWithRetry(operation);
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

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document)
        {
            var result = Insert(document.Id, document.Content);
            return new DocumentResult<T>(result, document.Id);
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
            var operation = new Add<T>(key, value, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            var operation = new Add<T>(key, value, null, _converter, _transcoder)
            {
                Expires = expiration
            };
            return SendWithRetry(operation);
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

        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
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
            var operation = new Delete(key, null, _converter, _transcoder);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas)
        {
            var operation = new Delete(key, null, _converter, _transcoder)
            {
                Cas = cas
            };
            return SendWithRetry(operation);
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

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        public IDocumentResult<T> GetDocument<T>(string id)
        {
            var result = Get<T>(id);
            return new DocumentResult<T>(result, id);
        }

        /// <summary>
        /// Gets a value for a given key from a Memcached Bucket on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            var operation = new Get<T>(key, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize)
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

        public IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration)
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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            var operation = new Increment(key, initial, delta, expiration, null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            var operation = new Decrement(key, initial, delta, expiration,  null, _converter, _transcoder);
            return SendWithRetry(operation);
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
            var operation = new Append<string>(key, value, _transcoder, null, _converter);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Append(string key, byte[] value)
        {
            var operation = new Append<byte[]>(key, value, _transcoder, null, _converter);
            return SendWithRetry(operation);
        }


        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<string> Prepend(string key, string value)
        {
            var operation = new Prepend<string>(key, value, _transcoder, null, _converter);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        public IOperationResult<byte[]> Prepend(string key, byte[] value)
        {
            var operation = new Prepend<byte[]>(key, value, _transcoder, null, _converter);
            return SendWithRetry(operation);
        }

        public System.Threading.Tasks.Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public System.Threading.Tasks.Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public Task<IViewResult<T>> QueryAsync<T>(IViewQuery query)
        {
            throw new NotSupportedException("This method is only supported on Couchbase Bucket (persistent) types.");
        }

        public IViewResult<T> Query<T>(IViewQuery query)
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

        public IBucketManager CreateManager(string username, string password)
        {
            throw new NotSupportedException();
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
                _clusterManager.DestroyBucket(this);
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
