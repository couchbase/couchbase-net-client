using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private IConfigInfo _configInfo;
        private volatile bool _disposed;

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
            Interlocked.Exchange(ref _configInfo, configInfo);
        }

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        public IOperationResult<T> Insert<T>(string key, T value)
        {
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var vBucket = (IVBucket)keyMapper.MapKey(key);
            var server = vBucket.LocatePrimary();

            var operation = new SetOperation<T>(key, value, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                _log.Debug(m => m("Requires retry {0}", key));
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
            var keyMapper = _configInfo.GetKeyMapper(Name);
            var vBucket = (IVBucket)keyMapper.MapKey(key);
            var server = vBucket.LocatePrimary();

            var operation = new GetOperation<T>(key, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                _log.Debug(m => m("Requires retry {0}", key));
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
        public IViewResult<T> Get<T>(IViewQuery query)
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
            var baseUri = server.GetBaseViewUri();
            return new ViewQuery(baseUri, development);
        }

        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket, design document and view.
        /// </summary>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(string designdoc, bool development)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri();
            return new ViewQuery(baseUri, designdoc, development);
        }
        /// <summary>
        /// Creates an instance of an object that implements <see cref="Couchbase.Views.IViewQuery"/>, which targets a given bucket and design document.
        /// </summary>
        /// <param name="designdoc">The design document that the View belongs to.</param>>
        /// <param name="view">The View to query.</param>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        public IViewQuery CreateQuery(string designdoc, string viewname, bool development)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri();
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
