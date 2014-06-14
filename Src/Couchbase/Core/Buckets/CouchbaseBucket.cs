using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents a persistent Couchbase Bucket and can be used for performing CRUD operations on documents,
    /// querying Views and executing N1QL queries.
    /// </summary>
    public sealed class CouchbaseBucket : IBucket, IConfigObserver
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;
        private static readonly object SyncObj = new object();

        internal CouchbaseBucket(IClusterManager clusterManager)
        {
            _clusterManager = clusterManager;
        }

        internal CouchbaseBucket(IClusterManager clusterManager, string bucketName)
        {
            _clusterManager = clusterManager;
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

                Log.Info(m=>m("Updated CouchbaseBucket - old config rev#{0} new config rev#{1}", 
                    old==null ? 0 : old.BucketConfig.Rev, 
                    _configInfo.BucketConfig.Rev ));
            }
        }

        IServer GetServer(string key, out IVBucket vBucket)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            vBucket = (IVBucket)keyMapper.MapKey(key);
            return vBucket.LocatePrimary();
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
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new SetOperation<T>(key, value, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
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
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new ReplaceOperation<T>(key, value, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
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
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new AddOperation<T>(key, value, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
        }

        /// <summary>
        /// For a given key, removes a document from the database.
        /// </summary>
        /// <param name="key">The unique key for indexing.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<object> Remove(string key)
        {
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new DeleteOperation(key, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
        }

        /// <summary>
        /// Gets a value for a given key.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Get<T>(string key)
        {
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new GetOperation<T>(key, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Info(m => m("Requires retry {0}", key));
            }
            return operationResult;
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
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new IncrementOperation(key, initial, delta, expiration, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
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
        public IOperationResult<long> Decrement(string key)
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
        public IOperationResult<long> Decrement(string key, ulong delta)
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
        public IOperationResult<long> Decrement(string key, ulong delta, ulong initial)
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
        public IOperationResult<long> Decrement(string key, ulong delta, ulong initial, uint expiration)
        {
            IVBucket vBucket;
            var server = GetServer(key, out vBucket);

            var operation = new DecrementOperation(key, initial, delta, expiration, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
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
            var server = _configInfo.GetServer();
            return server.Send<T>(query);
        }

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        public IQueryResult<T> Query<T>(string query)
        {
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
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(baseUri, development);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket, design document and view.
        /// </summary>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(bool development, string designdoc)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(baseUri, designdoc, development);
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
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri(Name);
            return new ViewQuery(baseUri, designdoc, viewname, development);
        }

        /// <summary>
        /// Performs a CCCP request for the latest server configuration if the passed in operationResult
        /// requires a config update do to a NMV.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the passed in operation.</typeparam>
        /// <param name="operationResult">The <see cref="IOperationResult{T}"/> to check.</param>
        /// <returns>True if the operation should be retried again with the new config.</returns>
        bool CheckForConfigUpdates<T>(IOperationResult<T> operationResult)
        {
            var requiresRetry = false;
            if (operationResult.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                var bucketConfig = ((OperationResult<T>)operationResult).GetConfig();
                if (bucketConfig != null)
                {
                    Log.Info(m => m("New config found {0}", bucketConfig.Rev));
                    _clusterManager.NotifyConfigPublished(bucketConfig);
                    requiresRetry = true;
                }
            }
            return requiresRetry;
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
        /// Closes this <see cref="CouchbaseBucket"/> instance, shutting down and releasing all resources, 
        /// removing it from it's <see cref="ClusterManager"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
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
