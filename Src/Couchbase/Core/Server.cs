using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a Couchbase Server node on the network.
    /// </summary>
    internal class Server : IServer
    {
        private static readonly ILog Log = LogManager.GetLogger<Server>();
        private readonly ClientConfiguration _clientConfiguration;
        private readonly IOStrategy _ioStrategy;
        private readonly INodeAdapter _nodeAdapter;
        private readonly ITypeTranscoder _typeTranscoder;
        private readonly IBucketConfig _bucketConfig;
        private uint _viewPort = 8092;
        private uint _queryPort = 8093;
        private volatile bool _disposed;
        private volatile bool _isDead;
        private volatile bool _timingEnabled;
        private volatile bool _isDown;
        private readonly Timer _heartBeatTimer;
        private string _cachedViewUrl;
        private string _cachedQueryUrl;

        public Server(IOStrategy ioStrategy, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration,
            IBucketConfig bucketConfig, ITypeTranscoder transcoder) :
                this(ioStrategy,
                    new ViewClient(new HttpClient(), new JsonDataMapper(clientConfiguration), bucketConfig,
                        clientConfiguration),
                    new QueryClient(new HttpClient(), new JsonDataMapper(clientConfiguration), clientConfiguration),
                    nodeAdapter, clientConfiguration, transcoder, bucketConfig)
        {
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, INodeAdapter nodeAdapter,
            ClientConfiguration clientConfiguration, ITypeTranscoder transcoder, IBucketConfig bucketConfig)
        {
            _ioStrategy = ioStrategy;
            _ioStrategy.ConnectionPool.Owner = this;
            _nodeAdapter = nodeAdapter;
            _clientConfiguration = clientConfiguration;
            _timingEnabled = _clientConfiguration.EnableOperationTiming;
            _typeTranscoder = transcoder;
            _bucketConfig = bucketConfig;

            //services that this node is responsible for
            IsMgmtNode = _nodeAdapter.MgmtApi > 0;
            IsDataNode = _nodeAdapter.KeyValue > 0;
            IsQueryNode = _nodeAdapter.N1QL > 0;
            IsIndexNode = _nodeAdapter.IndexAdmin > 0;
            IsViewNode = _nodeAdapter.Views > 0;

            //View and query clients
            ViewClient = viewClient;
            QueryClient = queryClient;

            //timer and node status
            _heartBeatTimer = new Timer(1000)
            {
                Enabled = false
            };
            _heartBeatTimer.Elapsed += _heartBeatTimer_Elapsed;
            TakeOffline(_ioStrategy.ConnectionPool.InitializationFailed);
        }


        /// <summary>
        /// Gets a value indicating whether this instance is MGMT node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is MGMT node; otherwise, <c>false</c>.
        /// </value>
        public bool IsMgmtNode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is query node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is query node; otherwise, <c>false</c>.
        /// </value>
        public bool IsQueryNode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is data node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data node; otherwise, <c>false</c>.
        /// </value>
        public bool IsDataNode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is index node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is index node; otherwise, <c>false</c>.
        /// </value>
        public bool IsIndexNode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is view node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is view node; otherwise, <c>false</c>.
        /// </value>
        public bool IsViewNode { get; private set; }


        /// <summary>
        /// Gets or sets the SASL factory for authenticating each TCP connection.
        /// </summary>
        /// <value>
        /// The sasl factory.
        /// </value>
        public Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> SaslFactory { get; set; }

        /// <summary>
        /// Gets the remote <see cref="IPEndPoint"/> of this node.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public IPEndPoint EndPoint
        {
            get { return _ioStrategy.EndPoint; }
        }

        /// <summary>
        /// Gets a reference to the connection pool thar this node is using.
        /// </summary>
        /// <value>
        /// The connection pool.
        /// </value>
        public IConnectionPool ConnectionPool
        {
            get { return _ioStrategy.ConnectionPool; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance node is sending
        /// and receiving data securely with TLS.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is secure; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecure
        {
            get { return _ioStrategy.IsSecure; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is dead; otherwise, <c>false</c>.
        /// </value>
        public bool IsDead
        {
            get { return _isDead; }
            set { _isDead = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is down.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is down; otherwise, <c>false</c>.
        /// </value>
        public bool IsDown
        {
            get { return _isDown; }
            set { _isDown = value; }
        }

        /// <summary>
        /// Gets the client used for sending N1QL requests to the N1QL service.
        /// </summary>
        /// <value>
        /// The query client.
        /// </value>
        public IQueryClient QueryClient { get; private set; }

        /// <summary>
        /// Gets the view client for sending View requests to the data service.
        /// </summary>
        /// <value>
        /// The view client.
        /// </value>
        public IViewClient ViewClient { get; private set; }

        private async void _heartBeatTimer_Elapsed(object sender, ElapsedEventArgs args)
        {
            Log.DebugFormat("Checking if node {0} is down: {1}", EndPoint, _isDown);
            _heartBeatTimer.Stop();
            if (_isDown)
            {
                IConnection connection = null;
                try
                {
                    //once we can connect, we need a sasl mechanism for auth
                    CreateSaslMechanismIfNotExists();

                    //if we have a sasl mechanism, we just try a noop
                    connection = _ioStrategy.ConnectionPool.Acquire();
                    var noop = new Noop(new DefaultTranscoder(), 1000);

                    var result = _ioStrategy.Execute(noop);
                    if (result.Success)
                    {
                        Log.DebugFormat("Successfully connected to {0}", EndPoint);
                        Log.DebugFormat("Checking if node {0} is down: {1}", EndPoint, _isDown);
                        TakeOffline(false);
                    }
                }
                catch (Exception e)
                {
                    //the node is down or unreachable
                    Log.Debug(e);
                }
                finally
                {
                    //will be null if the node is dead
                    if (connection != null)
                    {
                        if (_isDown)
                        {
                            if (connection.Socket.Connected)
                            {
                                connection.IsDead = false;
                            }
                            _heartBeatTimer.Start();
                        }
                        _ioStrategy.ConnectionPool.Release(connection);
                    }
                    else
                    {
                        _heartBeatTimer.Start();
                    }
                }
            }
        }

        /// <summary>
        /// Creates the sasl mechanism using the <see cref="SaslFactory" /> provided if it is null.
        /// </summary>
        public void CreateSaslMechanismIfNotExists()
        {
            if (_ioStrategy.SaslMechanism == null)
            {
                _ioStrategy.SaslMechanism = SaslFactory(
                    _bucketConfig.Name,
                    _bucketConfig.Password,
                    _ioStrategy,
                    _typeTranscoder);
            }
        }

        public void TakeOffline(bool isDown)
        {
            Log.DebugFormat("Taking node {0} offline: {1}", EndPoint, isDown);
            _isDown = isDown;
            if (_isDown)
            {
                _heartBeatTimer.Start();
            }
            else
            {
               _heartBeatTimer.Stop();
            }
        }

        IOperationResult HandleNodeUnavailable(IOperation operation)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            operation.Exception = new NodeUnavailableException(msg);
            operation.HandleClientError(msg, ResponseStatus.NodeUnavailable);
            return  operation.GetResult();
        }

        IOperationResult<T> HandleNodeUnavailable<T>(IOperation<T> operation)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            operation.Exception = new NodeUnavailableException(msg);
            operation.HandleClientError(msg, ResponseStatus.NodeUnavailable);
            return operation.GetResultWithValue();
        }

        /// <summary>
        /// Sends a key/value operation that contains no body to it's mapped server.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of the operation.
        /// </returns>
        public IOperationResult Send(IOperation operation)
        {
            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.BeginTimer(TimingLevel.Two);
            }

            IOperationResult result;
            if (_isDown)
            {
                result = HandleNodeUnavailable(operation);
            }
            else
            {
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
            }
            return result;
        }

        /// <summary>
        /// Sends a key/value operation that contains a body to it's mapped server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> representing the result of the operation.
        /// </returns>
        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.BeginTimer(TimingLevel.Two);
            }

            IOperationResult<T> result;
            if (_isDown)
            {
                result = HandleNodeUnavailable(operation);
            }
            else
            {
                try
                {
                    Log.Debug(m => m("Sending {0} using server {1}", operation.Key, EndPoint));
                    result = _ioStrategy.Execute(operation);
                }
                catch (Exception e)
                {
                    operation.Exception = e;
                    operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                    result = operation.GetResultWithValue();
                }

                if (Log.IsDebugEnabled && _timingEnabled)
                {
                    operation.EndTimer(TimingLevel.Two);
                }
            }
            return result;
        }

        /// <summary>
        /// Sends a key/value operation to it's mapped server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// A <see cref="Task" /> representing the asynchronous operation.
        /// </returns>
        public async Task SendAsync<T>(IOperation<T> operation)
        {
            if (_isDown)
            {
                var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

                operation.Completed(new SocketAsyncState
                {
                    Exception = new NodeUnavailableException(msg),
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.NodeUnavailable
                });
                return;
            }
            await _ioStrategy.ExecuteAsync(operation);
        }

        /// <summary>
        /// Sends a key/value operation that contains no body to it's mapped server asynchronously.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// A <see cref="Task" /> representing the asynchronous operation.
        /// </returns>
        public async Task SendAsync(IOperation operation)
        {
            if (_isDown)
            {
                var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

                operation.Completed(new SocketAsyncState
                {
                    Exception = new ServerException(msg),
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.NodeUnavailable
                });
                return;
            }
            await _ioStrategy.ExecuteAsync(operation);
        }

        /// <summary>
        /// Sends a request for a View to the server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row result.</typeparam>
        /// <param name="query">The <see cref="IViewQuery" /> representing the query.</param>
        /// <returns>
        /// An <see cref="Task{IViewResult}" /> object representing the asynchronous operation.
        /// </returns>
        public Task<IViewResult<T>> SendAsync<T>(IViewQuery query)
        {
            Task<IViewResult<T>> result;
            try
            {
                var baseUri = GetBaseViewUri(query.BucketName);
                query.BaseUri(baseUri);
                result = ViewClient.ExecuteAsync<T>(query);
            }
            catch (Exception e)
            {
                var tcs = new TaskCompletionSource<IViewResult<T>>();
                tcs.SetResult(new ViewResult<T>
                {
                    Exception = e,
                    Message = e.Message,
                    Error = e.Message,
                    Success = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    Rows = new List<ViewRow<T>>()
                });
                result = tcs.Task;
            }
            return result;
        }

        /// <summary>
        /// Sends a request for a View to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row result.</typeparam>
        /// <param name="query">The <see cref="IViewQuery" /> representing the query.</param>
        /// <returns>
        /// An <see cref="IViewResult{T}" /> representing the result of the query.
        /// </returns>
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

        /// <summary>
        /// Sends a request for a N1QL query to the server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest" /> object.</param>
        /// <returns>
        /// An <see cref="Task{IQueryResult}" /> object representing the asynchronous operation.
        /// </returns>
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

        /// <summary>
        /// Sends a request for a N1QL query to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest" /> object.</param>
        /// <returns></returns>
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

        public IQueryResult<IQueryPlan> Prepare(IQueryRequest toPrepare)
        {
            var uri = new Uri(GetBaseQueryUri());
            toPrepare.BaseUri(uri);
            return QueryClient.Prepare(toPrepare);
        }

        public IQueryResult<IQueryPlan> Prepare(string statementToPrepare)
        {
            IQueryRequest query = new QueryRequest(statementToPrepare);
            return Prepare(query);
        }

        public string GetBaseViewUri(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(_cachedQueryUrl))
            {
                const string uriPattern = @"{0}://{1}:{2}/{3}";
                var bucketConfig = _clientConfiguration.BucketConfigs[bucketName];

                _cachedViewUrl = string.Format(uriPattern,
                    bucketConfig.UseSsl ? "https" : "http",
                    _nodeAdapter.Hostname,
                    bucketConfig.UseSsl ? _nodeAdapter.ViewsSsl : _nodeAdapter.Views,
                    bucketName);
            }

            return _cachedViewUrl;
        }


        //TODO refactor to use CouchbaseApiHttps element when stabilized
        public string GetBaseViewUri2(string bucketName)
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
                uri = uri.Replace((_nodeAdapter.Views).
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
            sb.Append(_nodeAdapter.N1QL);
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

        private void Dispose(bool disposing)
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
