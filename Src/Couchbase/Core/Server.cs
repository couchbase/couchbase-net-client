using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
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
        private readonly Node _nodeInfo;
        private uint _viewPort = 8092;
        private uint _queryPort = 8093;
        private bool _disposed;

        public Server(IOStrategy ioStrategy, Node node, ClientConfiguration clientConfiguration) :
            this(ioStrategy,
            new ViewClient(new HttpClient(), new JsonDataMapper()),
            new QueryClient(new HttpClient(), new JsonDataMapper()),
            node, clientConfiguration)
        {
        }

        public Server(IOStrategy ioStrategy, Node node, ClientConfiguration clientConfiguration, IBucketConfig bucketConfig) :
            this(ioStrategy,
            new ViewClient(new HttpClient(), new JsonDataMapper(), bucketConfig),
            new QueryClient(new HttpClient(), new JsonDataMapper()),
            node, clientConfiguration)
        {

        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, Node nodeInfo, ClientConfiguration clientConfiguration)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
            QueryClient = queryClient;
            _nodeInfo = nodeInfo;
            _clientConfiguration = clientConfiguration;
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

        public bool IsDead { get; private set; }

        public IQueryClient QueryClient { get; private set; }

        public IViewClient ViewClient { get; private set; }

        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            IOperationResult<T> result;
            try
            {
                Log.Debug(m => m("Sending {0} using server {1}", operation.Key, EndPoint));
                result = _ioStrategy.Execute(operation);
            }
            catch (Exception e)
            {
                result = operation.GetResult();
                operation.Exception = e;
                operation.HandleClientError(e.Message);
            }
            return result;
        }

        public async Task<IViewResult<T>> SendAsync<T>(IViewQuery query)
        {
            return await ViewClient.ExecuteAsync<T>(query);
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
                    Rows = new List<T>()
                };
            }
            return result;
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
                    Rows = new List<T>()
                };
            }
            return result;
        }


        public async Task<IQueryResult<T>> SendAsync<T>(string query)
        {
            var uri = new Uri(GetBaseQueryUri());
            return await QueryClient.QueryAsync<T>(uri, query);
        }

        //note this should be cached
        public string GetBaseViewUri()
        {
            var uri = _nodeInfo.CouchApiBase;
            return uri.Replace("$HOST", "localhost");
        }

        //TODO refactor to use CouchbaseApiHttps element when stabilized
        public string GetBaseViewUri(string bucketName)
        {
            var uri = _nodeInfo.CouchApiBase;
            var index = uri.LastIndexOf("%", StringComparison.Ordinal);
            if (index > 0)
            {
                uri = uri.Substring(0, index);
            }

            var bucketConfig = _clientConfiguration.BucketConfigs[bucketName];
            if (bucketConfig.UseSsl)
            {
                var port = _nodeInfo.Ports.HttpsCapi;
                uri = uri.Replace(((int)DefaultPorts.RestApi).
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

        ~Server()
        {
            Log.Debug(m => m("Finalizing Server for {0}", EndPoint));
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
