using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Diagnostics;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core
{
    internal class Server : IServer
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfiguration;
        private readonly IOStrategy _ioStrategy;
        private readonly INodeAdapter _nodeAdapter;
        private uint _viewPort = 8092;
        private uint _queryPort = 8093;
        private volatile bool _disposed;
        private volatile bool _isDead;
        private volatile bool _timingEnabled;

        public Server(IOStrategy ioStrategy, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration, IBucketConfig bucketConfig) :
            this(ioStrategy,
           new ViewClient(new HttpClient(), new JsonDataMapper(clientConfiguration), bucketConfig, clientConfiguration),
           new QueryClient(new HttpClient(), new JsonDataMapper(clientConfiguration), clientConfiguration),
           nodeAdapter, clientConfiguration)
        {
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
            QueryClient = queryClient;
            _nodeAdapter = nodeAdapter;
            _clientConfiguration = clientConfiguration;
            _timingEnabled = _clientConfiguration.EnableOperationTiming;
        }

        public uint ViewPort
        {
            get { return _viewPort; }
            set { _viewPort = value; }
        }

        public uint QueryPort
        {
            get { return _queryPort; }
            set { _queryPort = value; }
        }

        public IPEndPoint EndPoint { get { return _ioStrategy.EndPoint; } }

        public IConnectionPool ConnectionPool { get { return _ioStrategy.ConnectionPool; } }

        public string HostName { get; set; }

        public uint DirectPort { get; private set; }

        public uint ProxyPort { get; private set; }

        public uint Replication { get; private set; }

        public bool Active { get; private set; }

        public bool Healthy { get; private set; }

        public bool IsSecure { get { return _ioStrategy.IsSecure; } }

        public bool IsDead
        {
            get { return _isDead; }
            set { _isDead = value; }
        }

        public IQueryClient QueryClient { get; private set; }

        public IViewClient ViewClient { get; private set; }

        public Task<IOperationResult<T>> SendAsync<T>(IOperation<T> operation)
        {
            return _ioStrategy.ExecuteAsync(operation);
        }

        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.BeginTimer(TimingLevel.Two);
            }

            IOperationResult<T> result;
            try
            {
                Log.Debug(m => m("Sending {0} using server {1}", operation.Key, EndPoint));
                result = _ioStrategy.Execute(operation);
            }
            catch (Exception e)
            {
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                result = operation.GetResult();
            }

            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.EndTimer(TimingLevel.Two);
            }
            return result;
        }

        public Task<IViewResult<T>> SendAsync<T>(IViewQuery query)
        {
            return ViewClient.ExecuteAsync<T>(query);
        }

        public IViewResult<T> Send<T>(IViewQuery query)
        {
            IViewResult<T> result;
            try
            {
                var baseUri = GetBaseViewUri(query.BucketName);
                query.BaseUri(baseUri);
                result = ViewClient.Execute<T>(query);
            }
            catch (Exception e)
            {
                result = new ViewResult<T>
                {
                    Exception = e,
                    Message = e.Message,
                    Error = e.Message,
                    Success = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    Rows = new List<ViewRow<T>>()
                };
            }
            return result;
        }

        IQueryResult<T> IServer.Send<T>(IQueryRequest queryRequest)
        {
            IQueryResult<T> result;
            try
            {
                if (queryRequest.GetBaseUri() == null)
                {
                    var uri = new Uri(GetBaseQueryUri());
                    queryRequest.BaseUri(uri);
                }
                result = QueryClient.Query<T>(queryRequest);
            }
            catch (Exception e)
            {
                result = new QueryResult<T>
                {
                    Exception = e,
                    Message = e.Message,
                    Success = false,
                };
            }
            return result;
        }

        Task<IQueryResult<T>> IServer.SendAsync<T>(IQueryRequest queryRequest)
        {
            if (queryRequest.GetBaseUri() == null)
            {
                var uri = new Uri(GetBaseQueryUri());
                queryRequest.BaseUri(uri);
            }
            return QueryClient.QueryAsync<T>(queryRequest);
        }

        IQueryResult<T> IServer.Send<T>(string query)
        {
            IQueryResult<T> result;
            try
            {
                var uri = new Uri(GetBaseQueryUri());
                result = QueryClient.Query<T>(uri, query);
            }
            catch (Exception e)
            {
                result = new QueryResult<T>
                {
                    Exception = e,
                    Message = e.Message,
                    Success = false,
                };
            }
            return result;
        }

        public Task<IQueryResult<T>> SendAsync<T>(string query)
        {
            var uri = new Uri(GetBaseQueryUri());
            var task = QueryClient.QueryAsync<T>(uri, query);
            return task;
        }

        //note this should be cached
        public string GetBaseViewUri()
        {
            var uri = _nodeAdapter.CouchbaseApiBase;
            return uri.Replace("$HOST", "localhost");
        }

        //TODO refactor to use CouchbaseApiHttps element when stabilized
        public string GetBaseViewUri(string bucketName)
        {
            var uri = _nodeAdapter.CouchbaseApiBase;
            var index = uri.LastIndexOf("%", StringComparison.Ordinal);
            if (index > 0)
            {
                uri = uri.Substring(0, index);
            }

            var bucketConfig = _clientConfiguration.BucketConfigs[bucketName];
            if (bucketConfig.UseSsl)
            {
                var port = _nodeAdapter.ViewsSsl;
                uri = uri.Replace(((int)DefaultPorts.CApi).
                    ToString(CultureInfo.InvariantCulture), port.
                    ToString(CultureInfo.InvariantCulture));
                uri = uri.Replace("http", "https");
            }

            return uri.Replace("$HOST", "localhost");
        }

        //TODO needs SSL support (when N1QL supports SSL)!
        public string GetBaseQueryUri()
        {
            var sb = new StringBuilder();
            sb.Append("http://");
            sb.Append(EndPoint.Address);
            sb.Append(":");
            sb.Append(QueryPort);
            sb.Append("/query");

            return sb.ToString();
        }

        public void MarkDead()
        {
            IsDead = true;
        }

        public void Dispose()
        {
            Log.Debug(m => m("Disposing Server for {0}", EndPoint));
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                MarkDead();
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_ioStrategy != null)
                {
                    _ioStrategy.Dispose();
                }
                _disposed = true;
            }
        }

#if DEBUG
        ~Server()
        {
            Log.Debug(m => m("Finalizing Server for {0}", EndPoint));
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
