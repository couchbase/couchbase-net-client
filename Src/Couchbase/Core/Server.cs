using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
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
using Timer = System.Timers.Timer;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a Couchbase Server node on the network.
    /// </summary>
    internal class Server : IServer
    {
        private static readonly ILog Log = LogManager.GetLogger<Server>();
        private readonly ClientConfiguration _clientConfiguration;
        private readonly BucketConfiguration _bucketConfiguration;
        private readonly IOStrategy _ioStrategy;
        private readonly INodeAdapter _nodeAdapter;
        private readonly ITypeTranscoder _typeTranscoder;
        private readonly IBucketConfig _bucketConfig;
        private volatile bool _disposed;
        private volatile bool _timingEnabled;
        private volatile bool _isDown;
        private readonly Timer _heartBeatTimer;
        private int _ioErrorCount;
        // ReSharper disable once InconsistentNaming
        private DateTime _lastIOErrorCheckedTime;
        private readonly object _syncObj = new object();

        public Server(IOStrategy ioStrategy, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration,
            IBucketConfig bucketConfig, ITypeTranscoder transcoder) :
            this(ioStrategy,
                    new ViewClient(new HttpClient(), new JsonDataMapper(clientConfiguration), bucketConfig, clientConfiguration),
                    new QueryClient(new HttpClient(), new JsonDataMapper(clientConfiguration), clientConfiguration),
                    nodeAdapter, clientConfiguration, transcoder, bucketConfig)
        {
        }

        public Server(IOStrategy ioStrategy, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration,
            IBucketConfig bucketConfig, ITypeTranscoder transcoder, ConcurrentDictionary<string, QueryPlan> queryCache) :
                this(ioStrategy,
                    new ViewClient(new HttpClient(), new JsonDataMapper(clientConfiguration), bucketConfig, clientConfiguration),
                    new QueryClient(new HttpClient(), new JsonDataMapper(clientConfiguration), clientConfiguration, queryCache),
                    nodeAdapter, clientConfiguration, transcoder, bucketConfig)
        {
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, INodeAdapter nodeAdapter,
            ClientConfiguration clientConfiguration, ITypeTranscoder transcoder, IBucketConfig bucketConfig)
        {
            if (ioStrategy != null)
            {
                _ioStrategy = ioStrategy;
                _ioStrategy.ConnectionPool.Owner = this;
            }
            _nodeAdapter = nodeAdapter;
            _clientConfiguration = clientConfiguration;
            _bucketConfiguration = clientConfiguration.BucketConfigs[bucketConfig.Name];
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

            CachedViewBaseUri = UrlUtil.GetViewBaseUri(_nodeAdapter, _bucketConfiguration);
            CachedQueryBaseUri = UrlUtil.GetN1QLBaseUri(_nodeAdapter, _bucketConfiguration);

            if (IsDataNode)
            {
                _lastIOErrorCheckedTime = DateTime.Now;
                _isDown = _ioStrategy.ConnectionPool.InitializationFailed;
                Log.InfoFormat("Initialization {0} for node {1}", _isDown ? "failed" : "succeeded", EndPoint);

                //timer and node status
                _heartBeatTimer = new Timer(_clientConfiguration.NodeAvailableCheckInterval)
                {
                    Enabled = _isDown
                };
                _heartBeatTimer.Elapsed += _heartBeatTimer_Elapsed;
            }
        }

        /// <summary>
        /// The base <see cref="Uri"/> for building a View query request.
        /// </summary>
        public Uri CachedViewBaseUri { get; private set; }

        /// <summary>
        /// The base <see cref="Uri"/> for building a N1QL query request.
        /// </summary>
        public Uri CachedQueryBaseUri { get; private set; }

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
            get { return IsDataNode ? _ioStrategy.EndPoint : _nodeAdapter.GetIPEndPoint(); }
        }

        /// <summary>
        /// Gets a reference to the connection pool thar this node is using.
        /// </summary>
        /// <value>
        /// The connection pool.
        /// </value>
        public IConnectionPool ConnectionPool
        {
            get { return IsDataNode ? _ioStrategy.ConnectionPool : null; }
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
            get { return IsDataNode ? _ioStrategy.IsSecure : _clientConfiguration.UseSsl; }
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

        // ReSharper disable once InconsistentNaming
        public int IOErrorCount
        {
            get { return _ioErrorCount; }
        }

        private void _heartBeatTimer_Elapsed(object sender, ElapsedEventArgs args)
        {
            Log.InfoFormat("Checking if node {0} is down: {1}", EndPoint, _isDown);
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
                        Log.InfoFormat("Successfully connected and marking node {0} as up.", EndPoint);
                        _isDown = false;
                        _heartBeatTimer.Stop();
                    }
                    else
                    {
                        Log.InfoFormat("The node {0} is still down: {1}", EndPoint, result.Status);
                    }
                }
                catch (Exception e)
                {
                    Log.InfoFormat("The node {0} is still down: {1}", EndPoint, e.Message);
                    //the node is down or unreachable
                    _isDown = true;
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

        public void CheckOnline(bool isDead)
        {
            if (isDead && IsDataNode)
            {
                lock (_syncObj)
                {
                    var current = DateTime.Now;
                    var last = _lastIOErrorCheckedTime.AddMilliseconds(_clientConfiguration.IOErrorCheckInterval);
                    Interlocked.Increment(ref _ioErrorCount);

                    Log.InfoFormat("Checking if node {0} should be down - last: {1}, current: {2}, count: {3}",
                               EndPoint, last.TimeOfDay, current.TimeOfDay, _ioErrorCount);

                    if (_ioErrorCount > _clientConfiguration.IOErrorThreshold)
                    {
                        if(last < current)
                        {
                            Log.InfoFormat("Marking node {0} as down - last: {1}, current: {2}, count: {3}",
                               EndPoint, last.TimeOfDay, current.TimeOfDay, _ioErrorCount);

                            _isDown = true;
                            _heartBeatTimer.Start();
                        }
                        Interlocked.Exchange(ref _ioErrorCount, 0);
                        _lastIOErrorCheckedTime = DateTime.Now;
                    }
                }
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
        public Task<IViewResult<T>> SendAsync<T>(IViewQueryable query)
        {
            Task<IViewResult<T>> result;
            try
            {
                query.BaseUri(CachedViewBaseUri);
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
        public IViewResult<T> Send<T>(IViewQueryable query)
        {
            IViewResult<T> result;
            try
            {
                query.BaseUri(CachedViewBaseUri);
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
                queryRequest.BaseUri(CachedQueryBaseUri);
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
            queryRequest.BaseUri(CachedQueryBaseUri);
            return QueryClient.QueryAsync<T>(queryRequest);
        }

        IQueryResult<T> IServer.Send<T>(string query)
        {
            IQueryResult<T> result;
            try
            {
                result = QueryClient.Query<T>(CachedQueryBaseUri, query);
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
            return QueryClient.QueryAsync<T>(CachedQueryBaseUri, query);
        }

        public IQueryResult<QueryPlan> Prepare(IQueryRequest toPrepare)
        {
            toPrepare.BaseUri(CachedQueryBaseUri);
            return QueryClient.Prepare(toPrepare);
        }

        public IQueryResult<QueryPlan> Prepare(string statementToPrepare)
        {
            IQueryRequest query = new QueryRequest(statementToPrepare);
            return Prepare(query);
        }

        public void MarkDead()
        {
            IsDown = true;
            if (_heartBeatTimer != null)
            {
                _heartBeatTimer.Start();
            }
        }

        public void Dispose()
        {
            Log.Info(m => m("Disposing Server for {0}", EndPoint));
            Dispose(true);
        }

        /// <summary>
        /// Invalidates and clears the query cache. This method can be used to explicitly clear the internal N1QL query cache. This cache will
        /// be filled with non-adhoc query statements (query plans) to speed up those subsequent executions. Triggering this method will wipe
        /// out the complete cache, which will not cause an interruption but rather all queries need to be re-prepared internally. This method
        /// is likely to be deprecated in the future once the server side query engine distributes its state throughout the cluster.
        /// </summary>
        /// <returns>
        /// An <see cref="int" /> representing the size of the cache before it was cleared.
        /// </returns>
        public int InvalidateQueryCache()
        {
            return QueryClient.InvalidateQueryCache();
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
