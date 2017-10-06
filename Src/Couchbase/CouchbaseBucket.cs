using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.Core.Version;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Http;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.SubDocument;
using Couchbase.Management;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase
{
    /// <summary>
    /// Represents a persistent Couchbase Bucket and can be used for performing CRUD operations on documents,
    /// querying Views and executing N1QL queries.
    /// </summary>
    /// <seealso cref="Couchbase.Core.IBucket" />
    /// <seealso cref="Couchbase.Configuration.Server.Providers.IConfigObserver" />
    /// <seealso cref="Couchbase.IRefCountable" />
    /// <seealso cref="Couchbase.IQueryCacheInvalidator" />
    /// <seealso cref="Couchbase.Core.IO.SubDocument.ISubdocInvoker" />
    public sealed class CouchbaseBucket : IBucket, IConfigObserver, IRefCountable, IQueryCacheInvalidator, ISubdocInvoker
    {
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseBucket>();
        private readonly IClusterController _clusterController;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private readonly IByteConverter _converter;
        private readonly ITypeTranscoder _transcoder;
        private readonly uint _operationLifespanTimeout;
        private IRequestExecuter _requestExecuter;
        private readonly ConcurrentDictionary<uint, IOperation> _pending = new ConcurrentDictionary<uint, IOperation>();
        private readonly IAuthenticator _authenticator;

        /// <summary>
        /// Used for reference counting instances so that <see cref="IDisposable.Dispose"/> is only called by the last instance.
        /// </summary>
        private static readonly ConditionalWeakTable<IDisposable, RefCount> RefCounts = new ConditionalWeakTable<IDisposable, RefCount>();

        [UsedImplicitly]
        private sealed class RefCount
        {
            public int Count;
        }

        IConfigInfo IConfigObserver.ConfigInfo
        {
            get { return _configInfo; }
        }

        internal CouchbaseBucket(IClusterController clusterController, string bucketName, IByteConverter converter, ITypeTranscoder transcoder, IAuthenticator authenticator)
        {
            _clusterController = clusterController;
            _converter = converter;
            _transcoder = transcoder;
            Name = bucketName;

            //extract the default operation lifespan timeout from configuration.
            BucketConfiguration bucketConfig;
            _operationLifespanTimeout = _clusterController.Configuration.BucketConfigs.TryGetValue(bucketName, out bucketConfig)
                ? bucketConfig.DefaultOperationLifespan
                : _clusterController.Configuration.DefaultOperationLifespan;

            //the global timeout for all operations unless an overload with timeout is called
            GlobalTimeout = new TimeSpan(0, 0, 0, (int)_operationLifespanTimeout);

            //If ICluster.Authenticate was called.
            _authenticator = authenticator;
        }

        /// <summary>
        /// For unit testing purposes only
        /// </summary>
        internal CouchbaseBucket(IRequestExecuter requestExecuter, IByteConverter converter, ITypeTranscoder transcoder)
        {
            _requestExecuter = requestExecuter;
            _converter = converter;
            _transcoder = transcoder;
        }

        /// <summary>
        /// Returns type of the bucket. In this implementation the value is constant: Couchbase.
        /// </summary>
        public BucketTypeEnum BucketType
        {
            get
            {
                return BucketTypeEnum.Couchbase;
            }
        }

        /// <summary>
        /// The Bucket's name. You can view this from the Couchbase Management Console.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The default or globally set operation lifetime.
        /// </summary>
        private TimeSpan GlobalTimeout { get; set; }

        /// <summary>
        /// Returns the <see cref="ICluster"/> that this bucket belongs to
        /// </summary>
        public ICluster Cluster
        {
            get { return _clusterController != null ? _clusterController.Cluster : null; }
        }

        /// <summary>
        /// Gets the key mapper used to map document keys to servers.
        /// </summary>
        internal IKeyMapper GetKeyMapper()
        {
            return _configInfo != null
                ? _configInfo.GetKeyMapper()
                : null;
        }

        /// <summary>
        /// Called when a configuration update has occurred from the server.
        /// </summary>
        /// <param name="configInfo">The new configuration</param>
        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Log.Info("Config updated old/new: {0}, {1}",
                _configInfo != null ? _configInfo.BucketConfig.Rev :
                0, configInfo.BucketConfig.Rev);
            Interlocked.Exchange(ref _configInfo, configInfo);
            Interlocked.Exchange(ref _requestExecuter,
                new CouchbaseRequestExecuter(_clusterController, _configInfo, Name, _pending));
        }

        private IServer GetServer(string key, out IVBucket vBucket)
        {
            var keyMapper = _configInfo.GetKeyMapper();
            vBucket = (IVBucket)keyMapper.MapKey(key);
            return vBucket.LocatePrimary();
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
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Append<string>(key, value, vBucket, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info("Requires retry {0}", key);
            }
            return operationResult;
        }

        /// <summary>
        /// Checks for the existance of a given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists.</returns>
        public bool Exists(string key)
        {
            return Exists(key, GlobalTimeout);
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
            var observe = new Observe(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var result = _requestExecuter.SendWithRetry(observe);
            return result.Success && result.Value.KeyState != KeyState.NotFound
                   && result.Value.KeyState != KeyState.LogicalDeleted;
        }

        /// <summary>
        ///  Check for existence of a given key
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns> Returns the <see cref="IOperationResult"/> object containing Value as true if the key exists</returns>
        internal IOperationResult<bool> _Exists(string key)
        {
            var observe = new Observe(key, null, _transcoder, _operationLifespanTimeout)
            {
                BucketName = Name
            };
            var result = _requestExecuter.SendWithRetry(observe);
            OperationResult<bool> ret = new OperationResult<bool>
            {
                Success = result.Success,
                Status = result.Status,
                Message = result.Message,
                Exception = result.Exception,
                Durability = result.Durability,
                Cas = result.Cas,
                Id = key
            };
            if (result.Value.KeyState != KeyState.NotFound && result.Value.KeyState != KeyState.LogicalDeleted)
            {
                ret.Value = true;
            }
            return ret;
        }


        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>A <see cref="Task{boolean}"/> object representing the asynchronous operation.</returns>
        public async Task<bool> ExistsAsync(string key)
        {
            return await ExistsAsync(key, GlobalTimeout).ContinueOnAnyContext();
        }

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<bool> ExistsAsync(string key, TimeSpan timeout)
        {
            CheckDisposed();
            var observe = new Observe(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var result = await _requestExecuter.SendWithRetryAsync(observe).ContinueOnAnyContext();
            return result.Success && result.Value.KeyState != KeyState.NotFound
                   && result.Value.KeyState != KeyState.LogicalDeleted;
        }

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        ///  <returns>A <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        internal async Task<IOperationResult<bool>> _ExistsAsync(string key)
        {
            CheckDisposed();
            var observe = new Observe(key, null, _transcoder, _operationLifespanTimeout)
            {
                BucketName = Name
            };
            var result = await _requestExecuter.SendWithRetryAsync(observe).ContinueOnAnyContext();
            OperationResult<bool> ret = new OperationResult<bool>
            {
                Success = result.Success,
                Status = result.Status,
                Message = result.Message,
                Exception = result.Exception,
                Durability = result.Durability,
                Cas = result.Cas,
                Id = key
            };
            if (result.Value.KeyState != KeyState.NotFound && result.Value.KeyState != KeyState.LogicalDeleted)
            {
                ret.Value = true;
            }
            return ret;
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
            var operation = new Append<string>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
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
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Append<byte[]>(key, value, vBucket, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info("Requires retry {0}", key);
            }
            return operationResult;
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
            var operation = new Append<byte[]>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var result = _requestExecuter.SendWithRetryAsync(operation);
            return result;
        }

        /// <summary>
        /// Creates a <see cref="BucketManager" /> instance for managing buckets.
        /// </summary>
        /// <param name="username">The administrators username</param>
        /// <param name="password">The administrators username</param>
        /// <returns>
        /// A <see cref="BucketManager" /> instance.
        /// </returns>
        public IBucketManager CreateManager(string username, string password)
        {
            return new BucketManager(this,
                _configInfo.ClientConfig,
                new JsonDataMapper(_configInfo.ClientConfig),
                new CouchbaseHttpClient(username, password),
                username,
                password);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        public IOperationResult Touch(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var touch = new Touch(key, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration.ToTtl(),
                BucketName = Name
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

        public Task<IOperationResult> TouchAsync(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var touch = new Touch(key, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration.ToTtl(),
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(touch);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="IViewQuery"/>, which targets a given bucket, design document and view.
        /// </summary>
        /// <param name="designDoc"></param>
        /// <param name="view"></param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(string designDoc, string view)
        {
            CheckDisposed();
            return new ViewQuery(Name, designDoc, view)
            {
                UseSsl = _configInfo.SslConfigured
            };
        }
        /// <summary>
        /// Creates an instance of an object that implements <see cref="IViewQuery"/>, which targets a given bucket and design document.
        /// </summary>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <param name="viewname"></param>
        /// <param name="development">True will execute on the development dataset.</param>
        /// >
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(string designdoc, string viewname, bool development)
        {
            CheckDisposed();
            return new ViewQuery(Name, designdoc, viewname)
            {
                UseSsl = _configInfo.SslConfigured
            }
            .Development(development) as IViewQuery;
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

            return Decrement(key, delta, initial, expiration, GlobalTimeout);
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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            return Decrement(key, delta, GlobalTimeout);
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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
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
            CheckDisposed();
            var operation = new Decrement(key, initial, delta, expiration, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
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
            var operation = new Decrement(key, initial, delta, expiration, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
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
            CheckDisposed();
            var operation = new Decrement(key, initial, delta, expiration.ToTtl(), null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Closes this <see cref="CouchbaseBucket"/> instance, shutting down and releasing all resources,
        /// removing it from it's <see cref="ClusterController"/> instance.
        /// </summary>
        public void Dispose()
        {
            Log.Debug("Attempting dispose on thread {0}", Thread.CurrentThread.ManagedThreadId);
            ((IRefCountable)this).Release();

        }

        /// <summary>
        /// Compares for equality which is the Name of the Bucket and it's <see cref="ClusterController"/> instance.
        /// </summary>
        /// <param name="obj">The other <see cref="CouchbaseBucket"/> reference to compare against.</param>
        /// <returns>True if they have the same name and <see cref="ClusterController"/> instance.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is CouchbaseBucket && Equals((CouchbaseBucket)obj);
        }

        public async Task<IDocumentResult<T>> GetDocumentAsync<T>(string id, TimeSpan timeout)
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

        /// <summary>
        /// Gets a list of documents by their given id as an asynchronous operation.
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
        /// Gets a document using a replica by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await GetFromReplicaAsync<T>(id).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        /// <summary>
        /// Gets a value for a given key.
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
            var operation = new Get<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key, TimeSpan timeout)
        {
            //the vbucket will be set in the IRequestExecuter - passing nulls should be refactored in the future
            var operation = new ReplicaRead<T>(key, null, _transcoder, timeout.GetSeconds());
            return _requestExecuter.ReadFromReplicaAsync(operation);
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
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        public IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize)
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
        ///  Gets a value for a given key as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="key">The documents primary key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            return GetAsync<T>(key, GlobalTimeout);
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
            var operation = new Get<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
        public async Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await GetAndTouchAsync<T>(key, expiration, timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
        }

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
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
        /// Gets a document by it's given id asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        public async Task<IDocumentResult<T>> GetDocumentAsync<T>(string id)
        {
            return await GetDocumentAsync<T>(id, GlobalTimeout);
        }

        /// <summary>
        /// Gets a document using a replica by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="IDocumentResult{T}"/></returns>
        public IDocumentResult<T> GetDocumentFromReplica<T>(string id)
        {
            return GetDocumentFromReplica<T>(id, GlobalTimeout);
        }

        /// <summary>
        /// Gets a document using a replica by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:Couchbase.IDocumentResult`1" />
        /// </returns>
        public IDocumentResult<T> GetDocumentFromReplica<T>(string id, TimeSpan timeout)
        {
            var result = GetFromReplica<T>(id, timeout);
            return new DocumentResult<T>(result);
        }

        /// <summary>
        /// Gets a document using a replica by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="Task{IDocumentResult{T}}"/> object representing the asynchronous operation.</returns>
        public Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id)
        {
            return GetDocumentFromReplicaAsync<T>(id, GlobalTimeout);
        }

        /// <summary>
        /// Gets a value for a key by checking each replica.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the value being retrieved.</typeparam>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>An <see cref="IOperationResult"/> with the results of the operation.</returns>
        public IOperationResult<T> GetFromReplica<T>(string key)
        {
            return GetFromReplica<T>(key, GlobalTimeout);
        }

        /// <summary>
        /// Returns a value for a
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        public IOperationResult<T> GetFromReplica<T>(string key, TimeSpan timeout)
        {
            //the vbucket will be set in the IRequestExecuter - passing nulls should be refactored in the future
            var operation = new ReplicaRead<T>(key, null, _transcoder, timeout.GetSeconds());
            return _requestExecuter.ReadFromReplica(operation);
        }

        /// <summary>
        /// Gets a value for a key by checking each replica asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the value being retrieved.</typeparam>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key)
        {
            return GetFromReplicaAsync<T>(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the hashcode for the CouchbaseBucket instance.
        /// </summary>
        /// <returns>The hashcode of the instance</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_clusterController != null ? _clusterController.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        /// <remarks>Expirations exceeding 30 seconds will be defaulted to 15 seconds.</remarks>
        /// <remarks>An expiration value of 0 will be defaulted to 15 seconds at the cluster.</remarks>
        [Obsolete("NCBC-1146: GetWithLock has been renamed to GetAndLock.")]
        public IOperationResult<T> GetWithLock<T>(string key, uint expiration)
        {
            const uint defaultExpiration = 15;
            const uint maxExpiration = 30;
            if (expiration > maxExpiration)
            {
                expiration = defaultExpiration;
            }
            var getl = new GetL<T>(key, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                Expiration = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(getl);
        }

        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration, TimeSpan timeout)
        {
            const uint defaultExpiration = 15;
            const uint maxExpiration = 30;
            if (expiration > maxExpiration)
            {
                expiration = defaultExpiration;
            }
            var getl = new GetL<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                Expiration = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(getl);
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> with the value.
        /// </returns>
        [Obsolete("NCBC-1146: GetWithLock has been renamed to GetAndLock.")]
        public IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration)
        {
            //note expiration.ToTtl() is not the best choice here since it enforces TTL limits which are
            //much higher than lock duration limits. Just convert to seconds and let overload do the checking.
            return GetWithLock<T>(key, (uint)expiration.TotalSeconds);
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        /// <remarks>Expirations exceeding 30 seconds will be defaulted to 15 seconds.</remarks>
        /// <remarks>An expiration value of 0 will be defaulted to 15 seconds at the cluster.</remarks>
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration)
        {
            return GetAndLock<T>(key, expiration, GlobalTimeout);
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
        public IOperationResult<T> GetAndLock<T>(string key, uint expiration, TimeSpan timeout)
        {
            const uint defaultExpiration = 15;
            const uint maxExpiration = 30;
            if (expiration > maxExpiration)
            {
                expiration = defaultExpiration;
            }
            var getl = new GetL<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                Expiration = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(getl);
        }

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> with the value.
        /// </returns>
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration)
        {
            //note expiration.ToTtl() is not the best choice here since it enforces TTL limits which are
            //much higher than lock duration limits. Just convert to seconds and let overload do the checking.
            return GetAndLock<T>(key, (uint)expiration.TotalSeconds);
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
        public IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return GetAndLock<T>(key, expiration.ToTtl(), timeout);
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
        [Obsolete("NCBC-1146: GetWithLockAsync has been renamed to GetAndLockAsync.")]
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, uint expiration)
        {
            const uint defaultExpiration = 15;
            const uint maxExpiration = 30;
            if (expiration > maxExpiration)
            {
                expiration = defaultExpiration;
            }
            var getl = new GetL<T>(key, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                Expiration = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(getl);
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
        [Obsolete("NCBC-1146: GetWithLockAsync has been renamed to GetAndLockAsync.")]
        public Task<IOperationResult<T>> GetWithLockAsync<T>(string key, TimeSpan expiration)
        {
            return GetWithLockAsync<T>(key, (uint)expiration.TotalSeconds);
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
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration)
        {
            return GetAndLockAsync<T>(key, expiration, GlobalTimeout);
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
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration)
        {
            return GetAndLockAsync<T>(key, (uint)expiration.TotalSeconds);
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
        public Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration, TimeSpan timeout)
        {
            return GetAndLockAsync<T>(key, expiration, timeout);
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
        public Task<IOperationResult> UnlockAsync(string key, ulong cas, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Unlock(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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
            const uint expiration = 0;//infinite - there is also a 'special' value -1: 'don't create if missing'

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

            return Increment(key, delta, initial, timeout);
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

            return IncrementAsync(key, delta, initial, timeout);
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
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
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
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Increment(key, initial, delta, vBucket, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name,
                Expires = expiration
            };
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info("Requires retry {0}", key);
            }
            return operationResult;
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
            return IncrementAsync(key, delta, initial, new TimeSpan(expiration), timeout);
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
            return Increment(key, delta, initial, expiration.ToTtl(), GlobalTimeout);
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
            return IncrementAsync(key, delta, 1, GlobalTimeout);
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
            return IncrementAsync(key, delta, initial, 0, GlobalTimeout);
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
            CheckDisposed();
            var operation = new Increment(key, initial, delta, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name,
                Expires = expiration.ToTtl()
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return ReplaceAsync(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
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

        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout)
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
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return InsertAsync(documents, replicateTo, GlobalTimeout);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(InsertAsync(doc, replicateTo, timeout)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns></returns>
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return InsertAsync(documents, replicateTo, persistTo, GlobalTimeout);
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
        public Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(InsertAsync(doc, replicateTo, persistTo, timeout)));
            return Task.WhenAll(tasks);
        }

        public async Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await InsertAsync<T>(document.Id, document.Content, document.Expiry.ToTtl(), replicateTo, persistTo, timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
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
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
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
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync<T>(operation);
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
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" /> failing if it exists as an asynchronous operation.
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
                var result = await InsertAsync<T>(document.Id, document.Content, document.Expiry.ToTtl(), timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
        /// Inserts a JSON document into the <see cref="T:Couchbase.Core.IBucket" />failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Insert(document, replicateTo, PersistTo.Zero, timeout);

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
            return InsertAsync(key, value, expiration, timeout);
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
            return Insert(key, value, replicateTo, PersistTo.Zero, GlobalTimeout);
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
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Insert(key, value, replicateTo, PersistTo.Zero, timeout);
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
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return InsertAsync(document, replicateTo, PersistTo.Zero, timeout);
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
            return Insert(document, replicateTo, persistTo, GlobalTimeout);
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
        public IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var result = Insert(document.Id, document.Content, document.Expiry.ToTtl(), replicateTo, persistTo, timeout);
            return new DocumentResult<T>(result, document);
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
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return InsertAsync(key, value, replicateTo, PersistTo.Zero, timeout);
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
            return Insert<T>(key, value, replicateTo, persistTo, GlobalTimeout);
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
        public IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
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
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync<T>(operation, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Insert(key, value, replicateTo, persistTo, GlobalTimeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync<T>(operation, false, replicateTo, persistTo);
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
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Insert(key, value, expiration.ToTtl(), replicateTo, persistTo, GlobalTimeout);
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
        public IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return Insert(key, value, expiration.ToTtl(), replicateTo, persistTo, timeout);
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
            CheckDisposed();
            var operation = new Add<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
        /// Inserts a JSON document into the <see cref="IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return InsertAsync(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket" />failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return InsertAsync(document, replicateTo, persistTo, GlobalTimeout);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return InsertAsync(key, value, replicateTo, PersistTo.Zero);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return InsertAsync(key, value, replicateTo, persistTo, GlobalTimeout);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return InsertAsync(key, value, expiration, replicateTo, persistTo, GlobalTimeout);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return InsertAsync(key, value, expiration.ToTtl(), replicateTo, persistTo, GlobalTimeout);
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
        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return InsertAsync(key, value, expiration.ToTtl(), replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Increments the reference counter for this <see cref="IBucket"/> instance.
        /// </summary>
        /// <returns>The current count of all <see cref="IBucket"/> references, or -1 if a reference could not be added because the bucket is disposed.</returns>
        int IRefCountable.AddRef()
        {
            lock (RefCounts)
            {
                if (!_disposed)
                {
                    var refCount = RefCounts.GetOrCreateValue(this);
                    Log.Debug("Creating bucket refCount# {0}", refCount.Count);
                    return Interlocked.Increment(ref refCount.Count);
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Decrements the reference counter and calls <see cref="IDisposable.Dispose"/> if the count is zero.
        /// </summary>
        /// <returns></returns>
        int IRefCountable.Release()
        {
            lock (RefCounts)
            {
                var refCount = RefCounts.GetOrCreateValue(this);
                if (refCount.Count > 0)
                {
                    Log.Debug("Current bucket refCount# {0}", refCount.Count);
                    Interlocked.Decrement(ref refCount.Count);
                    if (refCount.Count != 0) return refCount.Count;
                    Log.Debug("Removing bucket refCount# {0}", refCount.Count);
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
        public async Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var config = _configInfo.ClientConfig.BucketConfigs[Name];
            var observer = new KeyObserver(_pending, _configInfo, _clusterController, config.ObserveInterval, (int)timeout.GetSeconds());
            using (var cts = new CancellationTokenSource(config.ObserveTimeout))
            {
                var result = await observer.ObserveAsync(key, cas, deletion, replicateTo, persistTo, cts);
                return result ? ObserveResponse.DurabilitySatisfied : ObserveResponse.DurabilityNotSatisfied;
            }
        }

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied.
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

            var observer = new KeyObserver(_pending, _configInfo, _clusterController, config.ObserveInterval, config.ObserveTimeout);

            return observer.Observe(key, cas, deletion, replicateTo, persistTo)
                ? ObserveResponse.DurabilitySatisfied
                : ObserveResponse.DurabilityNotSatisfied;
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
        public ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var config = _configInfo.ClientConfig.BucketConfigs[Name];

            var observer = new KeyObserver(_pending, _configInfo, _clusterController, config.ObserveInterval, (int)timeout.GetSeconds());

            return observer.Observe(key, cas, deletion, replicateTo, persistTo)
                ? ObserveResponse.DurabilitySatisfied
                : ObserveResponse.DurabilityNotSatisfied;
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
        /// An <see cref="Task{ObserveResponse}" /> value indicating if the durability requirement were or were not met.
        /// </returns>
        public async Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            CheckDisposed();
            var config = _configInfo.ClientConfig.BucketConfigs[Name];
            var observer = new KeyObserver(_pending, _configInfo, _clusterController, config.ObserveInterval, config.ObserveTimeout);
            using (var cts = new CancellationTokenSource(config.ObserveTimeout))
            {
                var result = await observer.ObserveAsync(key, cas, deletion, replicateTo, persistTo, cts);
                return result ? ObserveResponse.DurabilitySatisfied : ObserveResponse.DurabilityNotSatisfied;
            }
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
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Prepend<string>(key, value, vBucket, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info("Requires retry {0}", key);
            }
            return operationResult;
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
            var operation = new GetT<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration.ToTtl(),
                BucketName = Name
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
            var operation = new GetT<T>(key, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration.ToTtl(),
                BucketName = Name
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
            var operation = new Prepend<string>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
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
            CheckDisposed();
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new Prepend<byte[]>(key, value, vBucket, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult, operation))
            {
                Log.Info("Requires retry {0}", key);
            }
            return operationResult;
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
            var operation = new Prepend<byte[]>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
        }

        /// <summary>
        /// Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> used to generate the results.</param>
        /// <returns>An instance of an object that implements the <see cref="T:Couchbase.Views.IViewResult{T}"/> Type with the results of the query.</returns>
        /// <remarks>Use one of the IBucket.CreateQuery overloads to generate the query.</remarks>
        public IViewResult<T> Query<T>(IViewQueryable query)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetry<T>(query);
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
            return _requestExecuter.SendWithRetry<T>(new QueryRequest(query));
        }

        /// <summary>
        /// Executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetry<T>(queryRequest);
        }

        /// <summary>
        /// Asynchronously Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> used to generate the results.</param>
        /// <returns>An awaitable <see cref="Task{T}"/> with the T a <see cref="IViewResult{T}"/> instance.</returns>
        /// <remarks>Note this implementation is experimental and subject to change in future release!</remarks>
        public Task<IViewResult<T>> QueryAsync<T>(IViewQueryable query)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetryAsync<T>(query);
        }

        /// <summary>
        /// Asynchronously executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        /// <remarks>Note this implementation is uncommitted/experimental and subject to change in future release!</remarks>
        public Task<IQueryResult<T>> QueryAsync<T>(string query)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetryAsync<T>(new QueryRequest(query), CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            return QueryAsync<T>(queryRequest, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetryAsync<T>(queryRequest, cancellationToken);
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

        /// <summary>
        /// Removes a list of <see cref="T:Couchbase.IDocument" /> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A list of <see cref="T:System.Threading.Tasks.Task`1" /> objects representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IOperationResult>>();
            documents.ForEach(doc => tasks.Add(RemoveAsync(doc, replicateTo, persistTo, timeout)));
            return Task.WhenAll(tasks);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, TimeSpan timeout)
        {
            const ulong cas = 0;
            return Remove(key, cas, timeout);
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
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
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
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas
            };
            return _requestExecuter.SendWithRetry(operation);
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
            return RemoveAsync(document.Id, timeout);
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
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Remove(document, replicateTo, PersistTo.Zero, timeout);
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
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync(operation);
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
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Remove(key, replicateTo, PersistTo.Zero, timeout);
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
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Remove(key, cas, replicateTo, PersistTo.Zero, timeout);
        }

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(document.Id, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, PersistTo.Zero);
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
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return Remove(document.Id, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, PersistTo.Zero);
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
            return Remove(key, replicateTo, persistTo, GlobalTimeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, true, replicateTo, persistTo);
        }

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, persistTo);
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
            return Remove(key, cas, replicateTo, persistTo, GlobalTimeout);
        }

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, true, replicateTo, persistTo);
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
        public Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, persistTo);
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
            return Remove(keys, GlobalTimeout);
        }

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
                        var result = Remove(key, timeout.GetSeconds());
                        results.TryAdd(key, result);
                    }
                });
            }
            return results;
        }

        /// <summary>
        /// Asynchronously removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult> RemoveAsync(string key)
        {
            return RemoveAsync(key, GlobalTimeout);
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
            return RemoveAsync(document, replicateTo, GlobalTimeout);
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
            return RemoveAsync(document, replicateTo, persistTo, GlobalTimeout);
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
        public Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Delete(document.Id, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync(operation, true, replicateTo, persistTo);
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
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return RemoveAsync(documents, replicateTo, GlobalTimeout);
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
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IOperationResult>>();
            documents.ForEach(doc => tasks.Add(RemoveAsync(doc, replicateTo, timeout)));
            return Task.WhenAll(tasks);
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
        public Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return RemoveAsync(documents, replicateTo, persistTo, GlobalTimeout);
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
            return RemoveAsync(key, replicateTo, PersistTo.Zero, GlobalTimeout);
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
            return RemoveAsync(key, cas, replicateTo, GlobalTimeout);
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
            return RemoveAsync(key, replicateTo, persistTo, GlobalTimeout);
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
            return RemoveAsync(key, cas, replicateTo, persistTo, GlobalTimeout);
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
            CheckDisposed();
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
                        var result = Upsert(key, value, timeout);
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
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
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

        public async Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await ReplaceAsync<T>(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(),
                    replicateTo, persistTo, timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
            var operation = new Replace<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(operation);
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
            return ReplaceAsync(key, value, 0, expiration, timeout);
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
            var operation = new Replace<T>(key, value, cas, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, uint expiration)
        {
            return Replace(key, value, expiration, GlobalTimeout);
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
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
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
            return Replace(key, value, expiration, timeout);
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
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                Expires = expiration,
                BucketName = Name
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
            var operation = new Replace<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                Cas = cas,
                BucketName = Name
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

        public async Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await ReplaceAsync(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(),
                    timeout).ContinueOnAnyContext();

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
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return Replace(document, replicateTo, PersistTo.Zero);
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
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Replace(document, replicateTo, PersistTo.Zero, timeout);
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
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Replace(key, value, replicateTo, PersistTo.Zero, timeout);
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
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, 0, replicateTo, timeout);
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
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Replace(key, value, cas, replicateTo, PersistTo.Zero, timeout);
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
            var result = Replace(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), replicateTo, persistTo);
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var result = Replace(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), replicateTo, persistTo, timeout);
            return new DocumentResult<T>(result, document);
        }

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, cas, 0, replicateTo, PersistTo.Zero, timeout);
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
            return Replace(key, value, replicateTo, persistTo, GlobalTimeout);
        }

        /// <summary>
        /// Replaces the specified key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return Replace(key, value, 0, 0, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Replaces the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, 0, 0, replicateTo, persistTo, timeout);
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
            return Replace(key, value, cas, 0, replicateTo, persistTo, GlobalTimeout);
        }

        /// <summary>
        /// Replaces the specified key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo,
                    TimeSpan timeout)
        {
            return Replace(key, value, cas, 0, replicateTo, persistTo, timeout);
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
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return ReplaceAsync(key, value, cas, 0, replicateTo, persistTo, timeout);
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
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Replace(key, value, cas, expiration, replicateTo, persistTo,
                GlobalTimeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var operation = new Replace<T>(key, value, cas, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new IO.Operations.Replace<T>(key, value, null, _transcoder,
                timeout.GetSeconds())
            {
                Expires = expiration,
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync<T>(operation, false, replicateTo, persistTo);
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
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Replace(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, GlobalTimeout);
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
        public IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            return Replace(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, timeout);
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

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return ReplaceAsync(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return ReplaceAsync(document, replicateTo, PersistTo.Zero, timeout);
        }

        /// <summary>
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
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
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return ReplaceAsync(documents, replicateTo, GlobalTimeout);
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
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(ReplaceAsync(doc, replicateTo, timeout)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Replaces a list of <see cref="T:Couchbase.IDocument`1" /> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// A <see cref="T:System.Threading.Tasks.Task`1" /> list.
        /// </returns>
        public Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(ReplaceAsync(doc, replicateTo, persistTo)));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ReplaceAsync(document, replicateTo, persistTo, GlobalTimeout);
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
            var operation = new Replace<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
            };
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
            return ReplaceAsync(key, value, cas, (uint) 0);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return ReplaceAsync(key, value, 0, 0, replicateTo, PersistTo.Zero);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo)
        {
            return ReplaceAsync(key, value, cas, 0, replicateTo, PersistTo.Zero);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ReplaceAsync(key, value, 0, 0, replicateTo, persistTo);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ReplaceAsync(key, value, cas, 0, replicateTo, persistTo);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ReplaceAsync(key, value, cas, expiration, replicateTo, persistTo,
                GlobalTimeout);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ReplaceAsync(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, GlobalTimeout);
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetWithLock{T}"/>.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>An <see cref="IOperationResult"/> with the status.</returns>
        public IOperationResult Unlock(string key, ulong cas)
        {
            return Unlock(key, cas, GlobalTimeout);
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
        public IOperationResult Unlock(string key, ulong cas, TimeSpan timeout)
        {
            var unlock = new Unlock(key, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(unlock);
        }

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetWithLock{T}"/> as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        public Task<IOperationResult> UnlockAsync(string key, ulong cas)
        {
            return UnlockAsync(key, cas, GlobalTimeout);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IResult{T}"/> with information regarding the operation.</returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document)
        {
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl());
            return new DocumentResult<T>(result, document);
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
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl());
            return new DocumentResult<T>(result, document);
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
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(UpsertAsync(doc, replicateTo, persistTo, timeout)));
            return Task.WhenAll(tasks);
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
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds())
            {
                BucketName = Name
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
            return UpsertAsync(key, value, 0, expiration, timeout);
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
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
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
            const int cas = 0;
            return Upsert(key, value, cas, expiration, timeout);
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
            const int cas = 0;
            return UpsertAsync(key, value, cas, expiration, timeout);
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
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout)
        {
            return Upsert(key, value, expiration.ToTtl(), timeout);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
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
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Cas = cas,
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetry(operation);
        }

        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithRetryAsync<T>(operation);
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
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// The <see cref="T:System.Threading.Tasks.Task`1" /> object representing the asynchronous operation.
        /// </returns>
        public async Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await UpsertAsync(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), timeout)
                    .ContinueOnAnyContext();

                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
        /// Inserts or replaces an existing JSON document into <see cref="T:Couchbase.Core.IBucket" /> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="T:Couchbase.IDocument`1" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing <see cref="T:Couchbase.IDocumentResult`1" /> with information regarding the operation.
        /// </returns>
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Upsert(document, replicateTo, PersistTo.Zero, timeout);
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
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An object implementing the <see cref="T:Couchbase.IOperationResult`1" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return Upsert(key, value, replicateTo, PersistTo.Zero, timeout);
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
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return UpsertAsync(document, replicateTo, PersistTo.Zero, timeout);
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
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), replicateTo, persistTo);
            return new DocumentResult<T>(result, document);
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
        public IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var result = Upsert(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(), replicateTo, persistTo, timeout);
            return new DocumentResult<T>(result, document);
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
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout)
        {
            return UpsertAsync(key, value, replicateTo, PersistTo.Zero, timeout);
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
            return Upsert(key, value, replicateTo, persistTo, GlobalTimeout);
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
        public IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Upsert(key, value, expiration, replicateTo, persistTo, GlobalTimeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {

            CheckDisposed();
            var operation = new IO.Operations.Set<T>(key, value, null, _transcoder,
                timeout.GetSeconds())
            {
                Expires = expiration,
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurabilityAsync<T>(operation, false, replicateTo, persistTo);
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
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Upsert(key, value, expiration.ToTtl(), replicateTo, persistTo);
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
        public IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return Upsert(key, value, expiration.ToTtl(), replicateTo, persistTo, timeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return UpsertAsync(key, value, 0, expiration, replicateTo, persistTo, timeout);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Upsert(key, value, cas, expiration, replicateTo, persistTo, GlobalTimeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                Cas = cas,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
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
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return UpsertAsync(key, value, expiration.ToTtl(), 0, replicateTo, persistTo, timeout);
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
        /// An object implementing the <see cref="IOperationResult{T}" />interface.
        /// </returns>
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Upsert(key, value, cas, expiration.ToTtl(), replicateTo, persistTo);
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
        public IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo,
            PersistTo persistTo, TimeSpan timeout)
        {
            return Upsert(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, timeout);
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
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            return UpsertAsync(key, value, cas, expiration.ToTtl(), replicateTo, persistTo, timeout);
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
            return Upsert(items, GlobalTimeout);
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
            CheckDisposed();
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
                        var result = Upsert(key, value, timeout);
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
            return Upsert(items, options, GlobalTimeout);
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
        /// <remarks>
        /// An item is <see cref="T:System.Collections.Generic.KeyValuePair`2" /> where K is a <see cref="T:System.String" /> and V is the <see cref="T:System.Type" />of the value use wish to store.
        /// </remarks>
        public IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, TimeSpan timeout)
        {
            CheckDisposed();
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
            return Upsert(items, options, rangeSize, GlobalTimeout);
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
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo)
        {
            return UpsertAsync(document, replicateTo, PersistTo.Zero);
        }

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket" /> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}" /> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>
        /// The <see cref="Task{IDocumentResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(document, replicateTo, persistTo, GlobalTimeout);
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
        public async Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<IDocumentResult<T>>();
            try
            {
                var result = await UpsertAsync(document.Id, document.Content, document.Cas, document.Expiry.ToTtl(),
                    replicateTo, persistTo, timeout).ContinueOnAnyContext();
                tcs.SetResult(new DocumentResult<T>(result, document));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return await tcs.Task.ContinueOnAnyContext();
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
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo)
        {
            return UpsertAsync(documents, replicateTo, GlobalTimeout);
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
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout)
        {
            var tasks = new List<Task<IDocumentResult<T>>>();
            documents.ForEach(doc => tasks.Add(UpsertAsync(doc, replicateTo, timeout)));
            return Task.WhenAll(tasks);
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
        public Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(documents, replicateTo, persistTo, GlobalTimeout);
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
            var operation = new Set<T>(key, value, null, _transcoder, GlobalTimeout.GetSeconds());
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
            return UpsertAsync(key, value, expiration.ToTtl());
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo)
        {
            return UpsertAsync(key, value, 0, 0, replicateTo, PersistTo.Zero);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(key, value, 0, 0, replicateTo, persistTo);
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
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout)
        {
            return UpsertAsync(key, value, 0, 0, replicateTo, persistTo, timeout);
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
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo,
            TimeSpan timeout)
        {
            CheckDisposed();
            var operation = new Set<T>(key, value, null, _transcoder, timeout.GetSeconds())
            {
                Expires = expiration,
                BucketName = Name
            };
            return _requestExecuter.SendWithDurability(operation, false, replicateTo, persistTo);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(key, value, 0, expiration, replicateTo, persistTo);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(key, value, cas, expiration, replicateTo, persistTo, GlobalTimeout);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(key, value, 0, expiration.ToTtl(), replicateTo, persistTo);
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
        /// The <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return UpsertAsync(key, value, cas, expiration.ToTtl(), replicateTo, persistTo);
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
        public bool SupportsEnhancedDurability
        {
            get { return _configInfo.SupportsEnhancedDurability; }
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
            get { return _configInfo.SupportsKvErrorMap; }
        }

        /// <summary>
        /// Gets a value indicating whether subdoc operations on xattributes are available.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports subdoc operations on xattributes; otherwise, <c>false</c>.
        /// </value>
        internal bool SupportsSubdocXAttributes
        {
            get { return _configInfo.SupportsSubdocXAttributes; }
        }

        /// <summary>
        /// Invalidates and clears the query cache. This method can be used to explicitly clear the internal N1QL query cache. This cache will
        /// be filled with non-adhoc query statements (query plans) to speed up those subsequent executions. Triggering this method will wipe
        /// out the complete cache, which will not cause an interruption but rather all queries need to be re-prepared internally. This method
        /// is likely to be deprecated in the future once the server side query engine distributes its state throughout the cluster.
        /// </summary>
        /// <returns>
        /// An <see cref="Int32" /> representing the size of the cache before it was cleared.
        /// </returns>
        public int InvalidateQueryCache()
        {
            return _configInfo.InvalidateQueryCache();
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
        public async Task<ClusterVersion?> GetClusterVersionAsync()
        {
            return await ClusterVersionProvider.Instance.GetVersionAsync(this);
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
                    var bucketConfig = operation.GetConfig(_clusterController.ServerConfigTranscoder);
                    if (bucketConfig != null)
                    {
                        Log.Info("New config found {0}|{1}: {2}", bucketConfig.Rev, _configInfo.BucketConfig.Rev, JsonConvert.SerializeObject(bucketConfig));
                        _clusterController.NotifyConfigPublished(bucketConfig);
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
        /// Closes this <see cref="CouchbaseBucket"/> instance, shutting down and releasing all resources,
        /// removing it from it's <see cref="ClusterController"/> instance.
        /// </summary>
        /// <param name="disposing">If true suppresses finalization.</param>
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Log.Debug("Disposing on thread {0}", Thread.CurrentThread.ManagedThreadId);
                if (_clusterController != null)
                {
                    _clusterController.DestroyBucket(this);
                }
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Compares for equality which is the Name of the Bucket and it's <see cref="ClusterController"/> instance.
        /// </summary>
        /// <param name="other">The other <see cref="CouchbaseBucket"/> reference to compare against.</param>
        /// <returns>True if they have the same name and <see cref="ClusterController"/> instance.</returns>
        private bool Equals(CouchbaseBucket other)
        {
            return Equals(_clusterController, other._clusterController) &&
                _disposed.Equals(other._disposed) &&
                string.Equals(Name, other.Name);
        }

#if DEBUG
        /// <summary>
        /// Finalizer for this <see cref="CouchbaseBucket"/> instance if not shutdown and disposed gracefully.
        /// </summary>
        ~CouchbaseBucket()
        {
            Dispose(false);
        }
#endif

        #region sub document api

        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key)
        {
            return new MutateInBuilder<TDocument>(this, _clusterController.Configuration.Serializer, key);
        }

        public IMutateInBuilder<TDocument> MutateIn<TDocument>(string key, TimeSpan timeout)
        {
            return new MutateInBuilder<TDocument>(this, _clusterController.Configuration.Serializer, key)
                .WithTimeout(timeout);
        }

        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key)
        {
            return new LookupInBuilder<TDocument>(this, _clusterController.Configuration.Serializer, key);
        }

        public ILookupInBuilder<TDocument> LookupIn<TDocument>(string key, TimeSpan timeout)
        {
            return new LookupInBuilder<TDocument>(this, _clusterController.Configuration.Serializer, key)
                .WithTimeout(timeout);
        }

        private SubDocSingularMutationBase<T> OptimizeSingleMutation<T>(MutateInBuilder<T> builder)
        {
            var spec = builder.FirstSpec();
            switch (spec.OpCode)
            {
                case OperationCode.SubArrayAddUnique:
                    return new SubArrayAddUnique<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()){ BucketName = Name };
                case OperationCode.SubArrayInsert:
                    return new SubArrayInsert<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubArrayPushFirst:
                    return new SubArrayPushFirst<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubArrayPushLast:
                    return new SubArrayPushLast<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubCounter:
                    return new SubCounter<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubDelete:
                    return new SubDocDelete<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubDictAdd:
                    return new SubDocDictAdd<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubDictUpsert:
                    return new SubDocDictUpsert<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubReplace:
                    return new SubDocReplace<T>(builder, builder.Key, null, _transcoder, GlobalTimeout.GetSeconds()) { BucketName = Name };
                default:
                    throw new NotSupportedException("Opcode is not supported for MutateInBuilder.");
            }
        }

        private static DocumentFragment<T> CreateXAttrsNotSuportedResponse<T>(string key, ITypeSerializerProvider provider)
        {
            return new DocumentFragment<T>(provider)
            {
                Id = key,
                Success = false,
                Exception = new FeatureNotAvailableException(ExceptionUtil.XAttriburesNotAvailableMessage),
                Status = ResponseStatus.ClientFailure
            };
        }

        public IDocumentFragment<T> Invoke<T>(IMutateInBuilder<T> builder)
        {
            var theBuilder = (MutateInBuilder<T>)builder;

            // Ensure we're not trying to use XATTRs with a cluster that doesn't support them
            if (theBuilder.ContainsXattrOperations && !_configInfo.SupportsSubdocXAttributes)
            {
                return CreateXAttrsNotSuportedResponse<T>(builder.Key, builder);
            }

            //optimize for the single operation
            if (builder.Count == 1)
            {
                return (DocumentFragment<T>)_requestExecuter.SendWithRetry(OptimizeSingleMutation(theBuilder));
            }

            var timeout = builder.Timeout.HasValue ? builder.Timeout.Value: GlobalTimeout;
            var multiMutate = new MultiMutation<T>(builder.Key, theBuilder, null, _transcoder, timeout.GetSeconds());
            return (DocumentFragment<T>)_requestExecuter.SendWithRetry(multiMutate);
        }

        public async Task<IDocumentFragment<T>> InvokeAsync<T>(IMutateInBuilder<T> builder)
        {
            var theBuilder = (MutateInBuilder<T>) builder;

            // Ensure we're not trying to use XATTRs with a cluster that doesn't support them
            if (theBuilder.ContainsXattrOperations && !_configInfo.SupportsSubdocXAttributes)
            {
                return CreateXAttrsNotSuportedResponse<T>(builder.Key, builder);
            }

            //optimize for the single operation
            if (builder.Count == 1)
            {
                return (DocumentFragment<T>)await _requestExecuter.SendWithRetryAsync(OptimizeSingleMutation(theBuilder)).ContinueOnAnyContext();
            }

            var timeout = builder.Timeout.HasValue ? builder.Timeout.Value : GlobalTimeout;
            var multiMutate = new MultiMutation<T>(builder.Key, theBuilder, null, _transcoder, timeout.GetSeconds());
            return (DocumentFragment<T>)await _requestExecuter.SendWithRetryAsync(multiMutate).ContinueOnAnyContext();
        }

        private SubDocSingularLookupBase<T> OptimizeSingleLookup<T>(LookupInBuilder<T> builder)
        {
            var timeout = builder.Timeout.HasValue ? builder.Timeout.Value : GlobalTimeout;
            var spec = builder.FirstSpec();

            switch (spec.OpCode)
            {
                case OperationCode.SubGet:
                    return new SubGet<T>(builder, builder.Key, null, _transcoder, timeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubExist:
                    return new SubExists<T>(builder, builder.Key, null, _transcoder, timeout.GetSeconds()) { BucketName = Name };
                case OperationCode.SubGetCount:
                    return new SubGetCount<T>(builder, builder.Key, null, _transcoder, _operationLifespanTimeout) { BucketName = Name };
                default:
                    throw new NotSupportedException("Opcode is not supported for LookupInBuilder.");
            }
        }

        public IDocumentFragment<T> Invoke<T>(ILookupInBuilder<T> builder)
        {
            var theBuilder = (LookupInBuilder<T>) builder;

            // Ensure we're not trying to use XATTRs with a cluster that doesn't support them
            if (theBuilder.ContainsXattrOperations && !_configInfo.SupportsSubdocXAttributes)
            {
                return CreateXAttrsNotSuportedResponse<T>(builder.Key, builder);
            }

            //optimize for the single operation
            if (theBuilder.Count == 1)
            {
                return (DocumentFragment<T>) _requestExecuter.SendWithRetry(OptimizeSingleLookup(theBuilder));
            }

            //this is a multi operation
            var timeout = builder.Timeout.HasValue ? builder.Timeout.Value : GlobalTimeout;
            var multiLookup = new MultiLookup<T>(builder.Key, theBuilder, null, _transcoder, timeout.GetSeconds()) { BucketName = Name };
            return (DocumentFragment<T>) _requestExecuter.SendWithRetry(multiLookup);
        }

        public async Task<IDocumentFragment<T>> InvokeAsync<T>(ILookupInBuilder<T> builder)
        {
            var theBuilder = (LookupInBuilder<T>) builder;

            // Ensure we're not trying to use XATTRs with a cluster that doesn't support them
            if (theBuilder.ContainsXattrOperations && !_configInfo.SupportsSubdocXAttributes)
            {
                return CreateXAttrsNotSuportedResponse<T>(builder.Key, builder);
            }

            //optimize for the single operation
            if (theBuilder.Count == 1)
            {
                return (DocumentFragment<T>)await _requestExecuter.SendWithRetryAsync(OptimizeSingleLookup(theBuilder)).ContinueOnAnyContext();
            }

            var timeout = builder.Timeout.HasValue ? builder.Timeout.Value : GlobalTimeout;
            var multiMutate = new MultiLookup<T>(builder.Key, theBuilder, null, _transcoder, timeout.GetSeconds()) { BucketName = Name };
            return (DocumentFragment<T>)await _requestExecuter.SendWithRetryAsync(multiMutate).ContinueOnAnyContext();
        }

        #endregion

        #region FTS

        public ISearchQueryResult Query(SearchQuery searchQuery)
        {
            return _requestExecuter.SendWithRetry(searchQuery);
        }

        public Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery)
        {
            return _requestExecuter.SendWithRetryAsync(searchQuery);
        }

        #endregion

        #region  Data Structures

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// The value as <see cref="IResult{TContent}" />
        /// </returns>
        public IResult<TContent> MapGet<TContent>(string key, string mapkey)
        {
            return MapGet<TContent>(key, mapkey, GlobalTimeout);
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
        public IResult<TContent> MapGet<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            var result = LookupIn<TContent>(key).WithTimeout(timeout).Get(mapkey).Execute();
            return new DefaultResult<TContent>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Content<TContent>(0)
            };
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult MapRemove(string key, string mapkey)
        {
            return MapRemove(key, mapkey, GlobalTimeout);
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
        public IResult MapRemove(string key, string mapkey, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).Remove(mapkey).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public IResult<int> MapSize(string key)
        {
            return MapSize(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public IResult<int> MapSize(string key, TimeSpan timeout)
        {
            var result = Get<Dictionary<string, object>>(key, timeout);
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult MapAdd(string key, string mapkey, string value, bool createMap)
        {
            return MapAdd(key, mapkey, value, createMap, GlobalTimeout);
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
        public IResult MapAdd(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).Insert(mapkey, value, createMap).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// The value as <see cref="IResult{TContent}" />
        /// </returns>
        public IResult<TContent> ListGet<TContent>(string key, int index)
        {
            return ListGet<TContent>(key, index, GlobalTimeout);
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
        public IResult<TContent> ListGet<TContent>(string key, int index, TimeSpan timeout)
        {
            var result = LookupIn<TContent>(key).Get(string.Format("[{0}]", index)).WithTimeout(timeout).Execute();
            return new DefaultResult<TContent>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Content<TContent>(0)
            };
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult ListAppend(string key, object value, bool createList)
        {
            return ListAppend(key, value, createList, GlobalTimeout);
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
        public IResult ListAppend(string key, object value, bool createList, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).ArrayAppend(value, createList).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult ListPrepend(string key, object value, bool createList)
        {
            return ListPrepend(key, value, createList, GlobalTimeout);
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
        public IResult ListPrepend(string key, object value, bool createList, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).ArrayPrepend(value, createList).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult ListRemove(string key, int index)
        {
            return ListRemove(key, index, GlobalTimeout);
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
        public IResult ListRemove(string key, int index, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).Remove(string.Format("[{0}]", index)).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult ListSet(string key, int index, string value)
        {
            return ListSet(key, index, value, GlobalTimeout);
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
        public IResult ListSet(string key, int index, string value, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).Replace(string.Format("[{0}]", index), value).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public IResult<int> ListSize(string key)
        {
            return ListSize(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public IResult<int> ListSize(string key, TimeSpan timeout)
        {
            var result = Get<List<object>>(key, timeout);
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult SetAdd(string key, string value, bool createSet)
        {
            return SetAdd(key, value, createSet, GlobalTimeout);
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
        public IResult SetAdd(string key, string value, bool createSet, TimeSpan timeout)
        {
            var result = MutateIn<object>(key).ArrayAddUnique(value, createSet).WithTimeout(timeout).Execute();
            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult{boolean}" /> with the operation result.
        /// </returns>
        public IResult<bool> SetContains(string key, string value)
        {
            return SetContains(key, value, GlobalTimeout);
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
        public IResult<bool> SetContains(string key, string value, TimeSpan timeout)
        {
            var result = Get<List<object>>(key, timeout);
            return new DefaultResult<bool>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success && result.Value.Contains(value)
            };
        }

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public IResult<int> SetSize(string key)
        {
            return SetSize(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public IResult<int> SetSize(string key, TimeSpan timeout)
        {
            var result = Get<List<object>>(key, timeout);
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public IResult SetRemove<T>(string key, T value)
        {
            return SetRemove(key, value, GlobalTimeout);
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
        public IResult SetRemove<T>(string key, T value, TimeSpan timeout)
        {
            const int maxAttempts = 10; //can be made a config option in a later commit
            var attempted = 0;
            do
            {
                var result = GetDocument<List<T>>(key, timeout);
                if (!result.Success)
                {
                    return new DefaultResult
                    {
                        Success = result.Success,
                        Exception = result.Exception,
                        Message = result.Message
                    };
                }

                var doc = result.Content;
                if (doc.Contains(value))
                {
                    doc.Remove(value);
                }
                var update = Upsert(new Document<List<T>>
                {
                    Id = key,
                    Content = doc,
                    Cas = result.Document.Cas
                }, timeout);

                //return on success or anything but a CAS mismatch
                if (update.Success || update.Status != ResponseStatus.KeyExists)
                {
                    return new DefaultResult
                    {
                        Success = update.Success,
                        Exception = update.Exception,
                        Message = update.Message
                    };
                }
                Thread.Sleep(100); //could be made a configurable in a later commit
            } while (attempted++ < maxAttempts);

            return new DefaultResult
            {
                Success = false,
                Exception = new TimeoutException(),
                Message = "Timed out waiting for CAS resolution."
            };
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        public IResult QueuePush<T>(string key, T value, bool createQueue = true)
        {
            return QueuePush(key, value, createQueue, GlobalTimeout);
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
        public IResult QueuePush<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            var appendResult = MutateIn<List<T>>(key).ArrayAppend(value).WithTimeout(timeout).Execute();
            if (appendResult.Success)
            {
                return new DefaultResult
                {
                    Success = true
                };
            }

            if (appendResult.Exception is DocumentDoesNotExistException && createQueue)
            {
                var insertResult = Insert(key, new List<T> { value }, timeout);
                if (insertResult.Success)
                {
                    return new DefaultResult
                    {
                        Success = true
                    };
                }

                if (insertResult.Exception is DocumentAlreadyExistsException)
                {
                    return QueuePush(key, value, true, timeout);
                }

                return new DefaultResult
                {
                    Success = false,
                    Exception = insertResult.Exception,
                    Message = insertResult.Message
                };
            }

            return new DefaultResult
            {
                Success = false,
                Exception = appendResult.Exception,
                Message = appendResult.Message
            };
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        public IResult<T> QueuePop<T>(string key)
        {
            return QueuePop<T>(key, GlobalTimeout);
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
        public IResult<T> QueuePop<T>(string key, TimeSpan timeout)
        {
            DefaultResult<T> result = null;
            var attempts = 0;

            while (attempts++ < 10)
            {
                var getResult = Get<List<T>>(key, timeout);
                if (!getResult.Success)
                {
                    result = new DefaultResult<T>
                    {
                        Success = false,
                        Message = getResult.Message,
                        Exception = getResult.Exception
                    };
                    break;
                }

                if (!getResult.Value.Any())
                {
                    result = new DefaultResult<T>
                    {
                        Success = false,
                        Message = "No items in queue"
                    };
                    break;
                }

                var item = getResult.Value.First();
                var mutateResult = MutateIn<List<T>>(key)
                    .Remove("[-1]")
                    .WithCas(getResult.Cas)
                    .WithTimeout(timeout)
                    .Execute();

                if (!mutateResult.Success && mutateResult.Exception is CasMismatchException)
                {
                    continue;
                }

                result = new DefaultResult<T>
                {
                    Success = mutateResult.Success,
                    Message = mutateResult.Message,
                    Exception = mutateResult.Exception,
                    Value = mutateResult.Success ? item : default(T)
                };
                break;
            }

            return result;
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <returns>An <see cref="IResult{T}"/> with the operation result.</returns>
        public IResult<int> QueueSize(string key)
        {
            return QueueSize(key, GlobalTimeout);
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public IResult<int> QueueSize(string key, TimeSpan timeout)
        {
            var result = Get<List<object>>(key, timeout);
            return new DefaultResult<int>
            {
                Success = result.Success,
                Message = result.Message,
                Exception = result.Exception,
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// The value as <see cref="IResult{TContent}" />
        /// </returns>
        public Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey)
        {
            return MapGetAsync<TContent>(key, mapkey, GlobalTimeout);
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
        public async Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey, TimeSpan timeout)
        {
            var result = await LookupIn<TContent>(key).Get(mapkey).
                WithTimeout(timeout).ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult<TContent>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Content<TContent>(0)
            };
        }

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> MapRemoveAsync(string key, string mapkey)
        {
            return MapRemoveAsync(key, mapkey, GlobalTimeout);
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
        public async Task<IResult> MapRemoveAsync(string key, string mapkey, TimeSpan timeout)
        {

            var result = await MutateIn<object>(key).Remove(mapkey).WithTimeout(timeout).
                ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Gets the size of a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public Task<IResult<int>> MapSizeAsync(string key)
        {
            return MapSizeAsync(key, GlobalTimeout);
        }

        public async Task<IResult<int>> MapSizeAsync(string key, TimeSpan timeout)
        {
            var result = await GetAsync<Dictionary<string, object>>(key, timeout).ContinueOnAnyContext();
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap)
        {
            return MapAddAsync(key, mapkey, value, createMap, GlobalTimeout);
        }

        public async Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).Insert(mapkey, value, createMap).WithTimeout(timeout)
                .ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Returns the value at a given index assuming a JSON array asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// The value as <see cref="IResult{TContent}" />
        /// </returns>
        public Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index)
        {
            return ListGetAsync<TContent>(key, index, GlobalTimeout);
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
        public async Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index, TimeSpan timeout)
        {
            var result = await LookupIn<TContent>(key).Get(string.Format("[{0}]", index)).WithTimeout(timeout)
                .ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult<TContent>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Content<TContent>(0)
            };
        }

        /// <summary>
        /// Appends a value to the back of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> ListAppendAsync(string key, object value, bool createList)
        {
            return ListAppendAsync(key, value, createList, GlobalTimeout);
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
        public async Task<IResult> ListAppendAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).ArrayAppend(value, createList).WithTimeout(timeout)
                .ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> ListPrependAsync(string key, object value, bool createList)
        {
            return ListPrependAsync(key, value, createList, GlobalTimeout);
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
        public async Task<IResult> ListPrependAsync(string key, object value, bool createList, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).ArrayPrepend(value, createList)
                .WithTimeout(timeout).ExecuteAsync();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Removes a value at a given index with a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> ListRemoveAsync(string key, int index)
        {
            return ListRemoveAsync(key, index, GlobalTimeout);
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
        public async Task<IResult> ListRemoveAsync(string key, int index, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).Remove(string.Format("[{0}]", index)).
                WithTimeout(timeout).ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> ListSetAsync(string key, int index, string value)
        {
            return ListSetAsync(key, index, value, GlobalTimeout);
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
        public async Task<IResult> ListSetAsync(string key, int index, string value, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).Replace(string.Format("[{0}]", index), value)
                .WithTimeout(timeout).ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public Task<IResult<int>> ListSizeAsync(string key)
        {
            return ListSizeAsync(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public async Task<IResult<int>> ListSizeAsync(string key, TimeSpan timeout)
        {
            var result = await GetAsync<List<object>>(key, timeout).ContinueOnAnyContext();
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Adds a value to a set within a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> SetAddAsync(string key, string value, bool createSet)
        {
            return SetAddAsync(key, value, createSet, GlobalTimeout);
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
        public async Task<IResult> SetAddAsync(string key, string value, bool createSet, TimeSpan timeout)
        {
            var result = await MutateIn<object>(key).ArrayAddUnique(value, createSet)
                .WithTimeout(timeout).ExecuteAsync().ContinueOnAnyContext();

            return new DefaultResult
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString()
            };
        }

        /// <summary>
        /// Checks if a set contains a given value within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult{boolean}" /> with the operation result.
        /// </returns>
        public Task<IResult<bool>> SetContainsAsync(string key, string value)
        {
            return SetContainsAsync(key, value, GlobalTimeout);
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
        public async Task<IResult<bool>> SetContainsAsync(string key, string value, TimeSpan timeout)
        {
            var result = await GetAsync<List<object>>(key, timeout).ContinueOnAnyContext();
            return new DefaultResult<bool>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success && result.Value.Contains(value)
            };
        }

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// A <see cref="IResult{integer}" /> with the operation result.
        /// </returns>
        public Task<IResult<int>> SetSizeAsync(string key)
        {
            return SetSizeAsync(key, GlobalTimeout);
        }

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.IResult`1" /> with the operation result.
        /// </returns>
        public async Task<IResult<int>> SetSizeAsync(string key, TimeSpan timeout)
        {
            var result = await GetAsync<List<object>>(key, timeout).ContinueOnAnyContext();
            return new DefaultResult<int>
            {
                Success = result.Success,
                Exception = result.Exception,
                Message = result.Status.ToString(),
                Value = result.Success ? result.Value.Count : 0
            };
        }

        /// <summary>
        /// Removes a value from a set withing a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="IResult" /> with the operation result.
        /// </returns>
        public Task<IResult> SetRemoveAsync<T>(string key, T value)
        {
            return SetRemoveAsync(key, value, GlobalTimeout);
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
        public async Task<IResult> SetRemoveAsync<T>(string key, T value, TimeSpan timeout)
        {
            const int maxAttempts = 10; //can be made a config option in a later commit
            var attempted = 0;
            do
            {
                var result = await GetDocumentAsync<List<T>>(key, timeout).ContinueOnAnyContext();
                if (!result.Success)
                {
                    return new DefaultResult
                    {
                        Success = result.Success,
                        Exception = result.Exception,
                        Message = result.Message
                    };
                }

                var doc = result.Content;
                if (doc.Contains(value))
                {
                    doc.Remove(value);
                }
                result = await UpsertAsync(new Document<List<T>>
                {
                    Content = doc,
                    Cas = result.Document.Cas,
                    Id = key
                }, timeout).ContinueOnAnyContext();
                if (result.Success || result.Status != ResponseStatus.KeyExists)
                {
                    return new DefaultResult
                    {
                        Success = result.Success,
                        Exception = result.Exception,
                        Message = result.Message
                    };
                }
                await Task.Delay(100); //could be made a configurable in a later commit
            } while (attempted++ < maxAttempts);

            return new DefaultResult
            {
                Success = false,
                Exception = new TimeoutException(),
                Message = "Timed out waiting for CAS resolution."
            };
        }

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        public Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue = true)
        {
            return QueuePushAsync(key, value, createQueue, GlobalTimeout);
        }

        public async Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue, TimeSpan timeout)
        {
            var appendResult = await MutateIn<List<T>>(key).ArrayAppend(value).WithTimeout(timeout).
                ExecuteAsync().ContinueOnAnyContext();

            if (appendResult.Success)
            {
                return new DefaultResult
                {
                    Success = true
                };
            }

            if (appendResult.Exception is DocumentDoesNotExistException && createQueue)
            {
                var insertResult = await InsertAsync(key, new List<T> { value }, timeout).ContinueOnAnyContext();
                if (insertResult.Success)
                {
                    return new DefaultResult
                    {
                        Success = true
                    };
                }

                if (insertResult.Exception is DocumentAlreadyExistsException)
                {
                    return await QueuePushAsync(key, value, true, timeout).ContinueOnAnyContext();
                }

                return new DefaultResult
                {
                    Success = false,
                    Exception = insertResult.Exception,
                    Message = insertResult.Message
                };
            }

            return new DefaultResult
            {
                Success = false,
                Exception = appendResult.Exception,
                Message = appendResult.Message
            };
        }

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        public Task<IResult<T>> QueuePopAsync<T>(string key)
        {
            return QueuePopAsync<T>(key, GlobalTimeout);
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
        public async Task<IResult<T>> QueuePopAsync<T>(string key, TimeSpan timeout)
        {
            DefaultResult<T> result = null;
            var attempts = 0;

            while (attempts++ < 10)
            {
                var getResult = await GetAsync<List<T>>(key, timeout).ContinueOnAnyContext();
                if (!getResult.Success)
                {
                    result = new DefaultResult<T>
                    {
                        Success = false,
                        Message = getResult.Message,
                        Exception = getResult.Exception
                    };
                    break;
                }

                if (!getResult.Value.Any())
                {
                    result = new DefaultResult<T>
                    {
                        Success = false,
                        Message = "No items in queue"
                    };
                    break;
                }

                var item = getResult.Value.First();
                var mutateResult = await MutateIn<List<T>>(key)
                    .Remove("[-1]")
                    .WithCas(getResult.Cas)
                    .WithTimeout(timeout)
                    .ExecuteAsync()
                    .ContinueOnAnyContext();

                if (!mutateResult.Success && mutateResult.Exception is CasMismatchException)
                {
                    continue;
                }

                result = new DefaultResult<T>
                {
                    Success = mutateResult.Success,
                    Message = mutateResult.Message,
                    Exception = mutateResult.Exception,
                    Value = mutateResult.Success ? item : default(T)
                };
                break;
            }

            return result;
        }

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        public Task<IResult<int>> QueueSizeAsync(string key)
        {
            return QueueSizeAsync(key, GlobalTimeout);
        }

        public async Task<IResult<int>> QueueSizeAsync(string key, TimeSpan timeout)
        {
            var result = await GetAsync<List<object>>(key, timeout).ContinueOnAnyContext();
            return new DefaultResult<int>
            {
                Success = result.Success,
                Message = result.Message,
                Exception = result.Exception,
                Value = result.Success ? result.Value.Count : 0
            };
        }

        #endregion

        #region CBAS

        /// <summary>
        /// Executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        public IAnalyticsResult<T> Query<T>(IAnalyticsRequest analyticsRequest)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetry<T>(analyticsRequest);
        }

        /// <summary>
        /// Asynchronously executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetryAsync<T>(analyticsRequest, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to stop the query being executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        public Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest, CancellationToken cancellationToken)
        {
            CheckDisposed();
            return _requestExecuter.SendWithRetryAsync<T>(analyticsRequest, cancellationToken);
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
