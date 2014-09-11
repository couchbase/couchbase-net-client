using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Annotations;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents a persistent Couchbase Bucket and can be used for performing CRUD operations on documents,
    /// querying Views and executing N1QL queries.
    /// </summary>
    public sealed class CouchbaseBucket : IBucket, IConfigObserver, IRefCountable
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private static readonly object SyncObj = new object();
        private readonly IByteConverter _converter;
        private readonly ITypeSerializer _serializer;

        /// <summary>
        /// Used for reference counting instances so that <see cref="IDisposable.Dispose"/> is only called by the last instance.
        /// </summary>
        private static readonly ConditionalWeakTable<IDisposable, RefCount> RefCounts = new ConditionalWeakTable<IDisposable, RefCount>();

        [UsedImplicitly]
        private sealed class RefCount
        {
            public int Count;
        }

        internal CouchbaseBucket(IClusterManager clusterManager, string bucketName, IByteConverter converter, ITypeSerializer serializer)
        {
            _clusterManager = clusterManager;
            _converter = converter;
            _serializer = serializer;
            Name = bucketName;
        }

        /// <summary>
        /// The Bucket's name. You can view this from the Couchbase Management Console.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Called when a configuration update has occurred from the server.
        /// </summary>
        /// <param name="configInfo">The new configuration</param>
        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Log.Info(m => m("Updating CouchbaseBucket - old config rev#{0} new config rev#{1} on thread {2}",
               _configInfo == null ? 0 : _configInfo.BucketConfig.Rev, 
                configInfo.BucketConfig.Rev, 
                Thread.CurrentThread.ManagedThreadId));

            lock (SyncObj)
            {
                var old = Interlocked.Exchange(ref _configInfo, configInfo);

                var old1 = old;
                Log.Info(m => m("Updated CouchbaseBucket - old config rev#{0} new config rev#{1} on thread {2}",
                    old1==null ? 0 : old1.BucketConfig.Rev,
                    _configInfo.BucketConfig.Rev,
                    Thread.CurrentThread.ManagedThreadId));
            }
        }

        IServer GetServer(string key, out IVBucket vBucket)
        {
            var keyMapper = _configInfo.GetKeyMapper();
            vBucket = (IVBucket) keyMapper.MapKey(key);
            return vBucket.LocatePrimary();
        }

        private IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            CheckDisposed();
            IOperationResult<T> operationResult;
            do
            {
                IVBucket vBucket;
                var server = GetServer(operation.Key, out vBucket);
                operation.VBucket = vBucket;
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
                    break;
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    retry = CheckForConfigUpdates(operationResult, operation);
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
                    retry = HandleIOError(operation, server) || supportsRetry;
                    break;
            }
            return retry;
        }

        bool HandleIOError<T>(IOperation<T> operation, IServer server)
        {
            var retry = false;
            var exception = operation.Exception as IOException;
            if (exception != null)
            {
                //Mark the current server as dead and force a reconfig
                server.MarkDead();

                var liveServer = _configInfo.GetServer();
                var result = liveServer.Send(new Config(_converter));
                if (result.Success)
                {
                    var config = result.Value;
                    if (config != null)
                    {
                        _clusterManager.NotifyConfigPublished(result.Value);
                    }
                }
            }
            return retry;
        }

        /// <summary>
        /// Performs a CCCP request for the latest server configuration if the passed in operationResult
        /// results in a NMV response.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the passed in operation.</typeparam>
        /// <param name="operationResult">The <see cref="IOperationResult{T}"/> to check.</param>
        /// <param name="operation"></param>
        /// <returns>True if the operation should be retried again with the new config.</returns>
        bool CheckForConfigUpdates<T>(IOperationResult<T> operationResult, IOperation operation)
        {
            var requiresRetry = false;
            if (operationResult.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                try
                {
                    var bucketConfig = operation.GetConfig();
                    if (bucketConfig != null)
                    {
                        if (bucketConfig.Rev > _configInfo.BucketConfig.Rev)
                        {
                            Log.Info(m => m("New config found {0}", bucketConfig.Rev));
                            var server = _configInfo.GetServer();

                            var result = server.Send(new Config(_converter));
                            if (result.Success)
                            {
                                var config = result.Value;
                                if (config != null)
                                {
                                    _clusterManager.NotifyConfigPublished(result.Value);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                requiresRetry = true;
            }
            return requiresRetry;
        }

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satified.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>A <see cref="ObserveResponse"/> value indicating if the durability requirement were or were not met.</returns>
        public ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            CheckDisposed();
            var config = _configInfo.ClientConfig.BucketConfigs[Name];
            var observer = new KeyObserver(_configInfo, config.ObserveInterval, config.ObserveTimeout);
            return observer.Observe(key, cas, deletion, replicateTo, persistTo)
                ? ObserveResponse.DurabilitySatisfied
                : ObserveResponse.DurabilityNotSatisfied;
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="IOperationResult{T}"/> with it's <see cref="Durability"/> status.</returns>
        private IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            CheckDisposed();
            var result = SendWithRetry(operation);
            if (result.Success)
            {
                var observeResponse = Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                result.Durability = observeResponse == ObserveResponse.DurabilitySatisfied
                    ? Durability.Satisfied
                    : Durability.NotSatisfied;
            }
            else
            {
                result.Durability = Durability.NotSatisfied;
            }
            return result;
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document)
        {
            var result = Upsert(document.Id, document.Value);
            return new DocumentResult<T>(result, document.Id);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value)
        {
            var operation = new Set<T>(key, value, null, _converter);
            return SendWithRetry(operation);
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
            const int expiration = 0;
            return Upsert(key, value, cas, expiration);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration)
        {
            const int cas = 0;
            return Upsert(key, value, cas, expiration);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration)
        {
            var operation = new Set<T>(key, value, null, _converter)
            {
                Cas = cas,
                Expires = expiration
            };
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return Upsert(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo)
        {
            return Upsert(key, value, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var result = Upsert(document.Id, document.Value, replicateTo, persistTo);
            return new DocumentResult<T>(result, document.Id);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Set<T>(key, value, null, _converter);
            return SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Set<T>(key, value, null, _converter)
            {
                Expires = expiration
            };
            return SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Set<T>(key, value, null, _converter)
            {
                Expires = expiration,
                Cas = cas
            };
            return SendWithDurability(operation, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document)
        {
            var result = Replace(document.Id, document.Value);
            return new DocumentResult<T>(result, document.Id);
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
            var operation = new Replace<T>(key, value, null, _converter, _serializer);
            return SendWithRetry(operation);
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
            var operation = new Replace<T>(key, value, cas, null, _converter, _serializer);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration)
        {
            var operation = new Replace<T>(key, value, null, _converter, _serializer)
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
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, uint cas, uint expiration)
        {
            var operation = new Replace<T>(key, value, null, _converter, _serializer)
            {
                Cas = cas,
                Expires = expiration
            };
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return Replace(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Replaces a value for a key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo)
        {
            return Replace(key, value, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Replaces a value for a key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas"></param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            return Replace(key, value, cas, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var result = Replace(document.Id, document.Value, replicateTo, persistTo);
            return new DocumentResult<T>(result, document.Id);
        }

        /// <summary>
        /// Replaces a value for a key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Replace<T>(key, value, null, _converter, _serializer);
            return SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Replace<T>(key, value,cas, null, _converter, _serializer);
            return SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Replace<T>(key, value, cas, null, _converter, _serializer)
            {
                Expires = expiration
            };
            return SendWithDurability(operation, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document)
        {
            var result = Insert(document.Id, document.Value);
            return new DocumentResult<T>(result, document.Id);
        }

        /// <summary>
        /// Inserts a document into the database using a given key, failing if the key exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value)
        {
            var operation = new Add<T>(key, value, null, _converter, _serializer);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration)
        {
            var operation = new Add<T>(key, value, null, _converter, _serializer)
            {
                Expires = expiration
            };
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return Insert(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts a document into the database using a given key, failing if the key exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo)
        {
            return Insert(key, value, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var result = Insert(document.Id, document.Value, replicateTo, persistTo);
            return new DocumentResult<T>(result, document.Id);
        }

        /// <summary>
        /// Inserts a document into the database using a given key, failing if the key exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Add<T>(key, value, null, _converter, _serializer);
            return SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Add<T>(key, value, null, _converter, _serializer)
            {
                Expires = expiration
            };
            return SendWithDurability(operation, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <returns>An object implementing <see cref="IResult"/> with information regarding the operation.</returns>
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
            const ulong cas = 0;
            return Remove(key, cas);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas)
        {
            var operation = new Delete(key, null, _converter, _serializer)
            {
                Cas = cas
            };
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IResult"/> with information regarding the operation.</returns>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return Remove(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ReplicateTo replicateTo)
        {
            return Remove(key, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo)
        {
            return Remove(key, cas, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IResult"/> with information regarding the operation.</returns>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Remove(document.Id, replicateTo, persistTo);
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Delete(key, null, _converter, _serializer);
            return SendWithDurability(operation, true, replicateTo, persistTo);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var operation = new Delete(key, null, _converter, _serializer)
            {
                Cas = cas
            };
            return SendWithDurability(operation, true, replicateTo, persistTo);
        }

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        public IDocumentResult<T> GetDocument<T>(string id)
        {
            var result = Get<T>(id);
            return new DocumentResult<T>(result, id);
        }

        /// <summary>
        /// Gets a value for a given key.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            var operation = new Get<T>(key, null, _converter, _serializer);
            return SendWithRetry(operation);
        }

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.  
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key)
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
        public IOperationResult<long> Increment(string key, ulong delta)
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
        public IOperationResult<long> Increment(string key, ulong delta, ulong initial)
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
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        public IOperationResult<long> Increment(string key, ulong delta, ulong initial, uint expiration)
        {
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Increment(key, initial, delta, expiration, vBucket, _converter, _serializer);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
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
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        public IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration)
        {
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Decrement(key, initial, delta, expiration, vBucket, _converter, _serializer);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
        }

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns></returns>
        public IOperationResult<string> Append(string key, string value)
        {
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Append<string>(key, value, _serializer , vBucket, _converter);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
        }

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns></returns>
        public IOperationResult<string> Prepend(string key, string value)
        {
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Prepend<string>(key, value, _serializer, vBucket, _converter);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
        }

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>A Task that can be awaited on for it's <see cref="IOperationResult{T}"/> value.</returns>
        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>A Task that can be awaited on for it's <see cref="IOperationResult{T}"/> value.</returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="Couchbase.Views.IViewQuery"/> used to generate the results.</param>
        /// <returns>An instance of an object that implements the <see cref="T:Couchbase.Views.IViewResult{T}"/> Type with the results of the query.</returns>
        /// <remarks>Use one of the IBucket.CreateQuery overloads to generate the query.</remarks>
        public IViewResult<T> Query<T>(IViewQuery query)
        {
            CheckDisposed();
            return SendWithRetry<T>((ViewQuery)query);
        }

        internal IViewResult<T> SendWithRetry<T>(ViewQuery query)
        {
            using (var cancellationTokenSource = new CancellationTokenSource(_configInfo.ClientConfig.ViewHardTimeout))
            {
                var task = RetryViewEvery((e, c) =>
                {
                    var server = c.GetServer();
                    return server.Send<T>(query);
                },
                query, _configInfo, cancellationTokenSource.Token);
                task.Wait(cancellationTokenSource.Token);
                return task.Result;
            }
        }

        static async Task<IViewResult<T>> RetryViewEvery<T>(Func<ViewQuery, IConfigInfo, IViewResult<T>> execute, ViewQuery query, IConfigInfo configInfo, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = execute(query, configInfo);
                if (query.RetryAttempts++ >= configInfo.ClientConfig.MaxViewRetries ||
                    result.Success ||
                    result.CannotRetry())
                {
                    return result;
                }
                Log.Debug(m=>m("trying again: {0}", query.RetryAttempts));
                var sleepTime = (int)Math.Pow(2, query.RetryAttempts);
                var task = Task.Delay(sleepTime, cancellationToken);
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    return result;
                }
            }
        }

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public IQueryResult<T> Query<T>(string query)
        {
            CheckDisposed();
            var server = _configInfo.GetServer();
            return server.Send<T>(query);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket.
        /// </summary>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(bool development)
        {
            CheckDisposed();
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(Name, baseUri, development);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket, design document and view.
        /// </summary>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(bool development, string designdoc)
        {
            CheckDisposed();
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(Name, baseUri, designdoc, development);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket and design document.
        /// </summary>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <param name="viewname"></param>
        /// >
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(bool development, string designdoc, string viewname)
        {
            CheckDisposed();
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(Name, baseUri, designdoc, viewname, development);
        }

        /// <summary>
        /// Compares for equality which is the Name of the Bucket and it's <see cref="ClusterManager"/> instance.
        /// </summary>
        /// <param name="other">The other <see cref="CouchbaseBucket"/> reference to compare against.</param>
        /// <returns>True if they have the same name and <see cref="ClusterManager"/> instance.</returns>
        private bool Equals(CouchbaseBucket other)
        {
            return Equals(_clusterManager, other._clusterManager) &&
                _disposed.Equals(other._disposed) &&
                string.Equals(Name, other.Name);
        }

        /// <summary>
        /// Gets the hashcode for the CouchbaseBucket instance.
        /// </summary>
        /// <returns>The hashcode of the instance</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_clusterManager != null ? _clusterManager.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                return hashCode;
            }
        }

        void CheckDisposed()
        {
            var message = string.Format("This bucket [{0}] has been disposed! Performing operations on a disposed bucket is not supported!", Name);
            if(_disposed) throw new ObjectDisposedException(message);
        }

        /// <summary>
        /// Compares for equality which is the Name of the Bucket and it's <see cref="ClusterManager"/> instance.
        /// </summary>
        /// <param name="obj">The other <see cref="CouchbaseBucket"/> reference to compare against.</param>
        /// <returns>True if they have the same name and <see cref="ClusterManager"/> instance.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is CouchbaseBucket && Equals((CouchbaseBucket) obj);
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
                Log.DebugFormat("Creating bucket refCount# {0}", refCount.Count);
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
                    Log.DebugFormat("Current bucket refCount# {0}", refCount.Count);
                    Interlocked.Decrement(ref refCount.Count);
                    if (refCount.Count != 0) return refCount.Count;
                    Log.DebugFormat("Removing bucket refCount# {0}", refCount.Count);
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
        /// Closes this <see cref="CouchbaseBucket"/> instance, shutting down and releasing all resources, 
        /// removing it from it's <see cref="ClusterManager"/> instance.
        /// </summary>
        public void Dispose()
        {
            Log.Debug(m => m("Attempting dispose on thread {0}", Thread.CurrentThread.ManagedThreadId));
            Release();
        }

        /// <summary>
        /// Closes this <see cref="CouchbaseBucket"/> instance, shutting down and releasing all resources, 
        /// removing it from it's <see cref="ClusterManager"/> instance.
        /// </summary>
        /// <param name="disposing">If true suppresses finalization.</param>
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Log.Debug(m => m("Disposing on thread {0}", Thread.CurrentThread.ManagedThreadId));
                _clusterManager.DestroyBucket(this);
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer for this <see cref="CouchbaseBucket"/> instance if not shutdown and disposed gracefully. 
        /// </summary>
        ~CouchbaseBucket()
        {
            Dispose(false);
        }
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
