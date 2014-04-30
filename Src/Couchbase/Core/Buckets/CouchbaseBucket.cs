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
    /// Represents a persistent Couchbase Bucket which is logical grouping of physical resources
    /// across a cluster.
    /// </summary>
    /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-admin/#data-storage"/>
    public sealed class CouchbaseBucket : ICouchbaseBucket, IConfigObserver
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

        public string Name { get; set; }

        void IConfigObserver.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Interlocked.Exchange(ref _configInfo, configInfo);
        }

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

        public Task<IOperationResult<T>> GetAsync<T>(string key)
        {
            Console.WriteLine("using thread {0}", Thread.CurrentThread.ManagedThreadId);

            var task = new Task<IOperationResult<T>>(() => Get<T>(key));
            task.Start();
            return task;
        }

        public Task<IOperationResult<T>> InsertAsync<T>(string key, T value)
        {
            throw new NotImplementedException();
        }

        public IViewResult<T> Get<T>(IViewQuery query)
        {
            var server = _configInfo.GetServer();
            return server.Send<T>(query);
        }

        public IQueryResult<T> Query<T>(string query)
        {
            var server = _configInfo.GetServer();
            return server.Send<T>(query);
        }

        public IViewQuery CreateQuery(bool development)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri();
            return new ViewQuery(baseUri, development);
        }

        public IViewQuery CreateQuery(string designdoc, bool development)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri();
            return new ViewQuery(baseUri, designdoc, development);
        }
         
        public IViewQuery CreateQuery(string designdoc, string viewname, bool development)
        {
            var server = _configInfo.GetServer();
            var baseUri = server.GetBaseViewUri();
            return new ViewQuery(baseUri, designdoc, viewname, development);
        }

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

        public void Dispose()
        {
            Dispose(true);
        }

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

        ~CouchbaseBucket()
        {
            Dispose(false);
        }
    }
}
