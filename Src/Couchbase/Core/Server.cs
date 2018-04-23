using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Http;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using Timer = System.Threading.Timer;

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
        private readonly IIOService _ioService;
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
        private readonly IQueryClient _streamingQueryClient;
        private readonly IViewClient _streamingViewClient;
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        //for log redaction
        private Func<object, string> User = RedactableArgument.UserAction;

        public Server(IIOService ioService, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration,
            IBucketConfig bucketConfig, ITypeTranscoder transcoder) :
            this(ioService, null, null, null, null, null, null, nodeAdapter, clientConfiguration, transcoder, bucketConfig)
        {
        }

        public Server(IIOService ioService, INodeAdapter nodeAdapter, ClientConfiguration clientConfiguration,
            IBucketConfig bucketConfig, ITypeTranscoder transcoder, ConcurrentDictionary<string, QueryPlan> queryCache) :
                this(ioService,
                    new ViewClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, clientConfiguration.ViewRequestTimeout)
                    }, new JsonDataMapper(clientConfiguration), clientConfiguration),
                    new StreamingViewClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, clientConfiguration.ViewRequestTimeout)
                    }, new JsonDataMapper(clientConfiguration), clientConfiguration),
                    new QueryClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, (int)clientConfiguration.QueryRequestTimeout)
                    }, new JsonDataMapper(clientConfiguration), clientConfiguration, queryCache),
                    new StreamingQueryClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, (int)clientConfiguration.QueryRequestTimeout)
                    }, new JsonDataMapper(clientConfiguration), clientConfiguration, queryCache),
                    new SearchClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, (int)clientConfiguration.SearchRequestTimeout)
                    }, new SearchDataMapper(), clientConfiguration),
                    new AnalyticsClient(new CouchbaseHttpClient(clientConfiguration, bucketConfig)
                    {
                        Timeout = new TimeSpan(0, 0, 0, 0, (int)clientConfiguration.QueryRequestTimeout)
                    }, new JsonDataMapper(clientConfiguration), clientConfiguration),
                    nodeAdapter, clientConfiguration, transcoder, bucketConfig)
        {
        }

        public Server(IIOService ioService, IViewClient viewClient, IViewClient streamingViewClient, IQueryClient queryClient, IQueryClient streamingQueryClient, ISearchClient searchClient,
            IAnalyticsClient analyticsClient, INodeAdapter nodeAdapter,
            ClientConfiguration clientConfiguration, ITypeTranscoder transcoder, IBucketConfig bucketConfig)
        {
            if (ioService != null)
            {
                _ioService = ioService;
                _ioService.ConnectionPool.Owner = this;
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
            IsSearchNode = _nodeAdapter.IsSearchNode;
            IsAnalyticsNode = _nodeAdapter.IsAnalyticsNode;

            //View and query clients
            ViewClient = viewClient;
            _streamingViewClient = streamingViewClient;
            QueryClient = queryClient;
            SearchClient = searchClient;
            _streamingQueryClient = streamingQueryClient;
            AnalyticsClient = analyticsClient;

            CachedViewBaseUri = UrlUtil.GetViewBaseUri(_nodeAdapter, _bucketConfiguration);
            CachedQueryBaseUri = UrlUtil.GetN1QLBaseUri(_nodeAdapter, _bucketConfiguration);

            if (IsDataNode || IsQueryNode)
            {
                _lastIOErrorCheckedTime = DateTime.Now;

                //On initialization, data nodes are authenticated, so they can start in a down state.
                //If the node is down immediately start the timer, otherwise disable it.
                if (IsDataNode)
                {
                    _isDown = _ioService.ConnectionPool.InitializationFailed;
                }

                Log.Info("Initialization {0} for node {1}", _isDown ? "failed" : "succeeded", EndPoint);

                //timer and node status
                _heartBeatTimer = new Timer(_heartBeatTimer_Elapsed, null, Timeout.Infinite, Timeout.Infinite);
                if (_isDown)
                {
                    StartHeartbeatTimer();
                }
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
        /// Gets a value indicating whether this instance is an analytics node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is analytics node; otherwise, <c>false</c>.
        /// </value>
        public bool IsAnalyticsNode { get; private set; }

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
        /// Gets a value indicating whether this instance is an FTS node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        public bool IsSearchNode { get; private set; }

        /// <summary>
        /// Gets the remote <see cref="IPEndPoint"/> of this node.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public IPEndPoint EndPoint
        {
            get { return IsDataNode ? _ioService.EndPoint : _nodeAdapter.GetIPEndPoint(); }
        }

        /// <summary>
        /// Gets a reference to the connection pool thar this node is using.
        /// </summary>
        /// <value>
        /// The connection pool.
        /// </value>
        public IConnectionPool ConnectionPool
        {
            get { return IsDataNode ? _ioService.ConnectionPool : null; }
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
            get { return IsDataNode ? _ioService.IsSecure : _clientConfiguration.UseSsl; }
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

        /// <summary>
        /// Gets the analytics client for sending Anlytics requests to the Analytics service.
        /// </summary>
        /// <value>
        /// The analytics client.
        /// </value>
        public IAnalyticsClient AnalyticsClient { get; private set; }

        /// <summary>
        /// Gets the <see cref="ISearchClient" /> for this node if <see cref="IsSearchNode" /> is <c>true</c>.
        /// </summary>
        /// <value>
        /// The search client.
        /// </value>
        public ISearchClient SearchClient { get; private set; }

        // ReSharper disable once InconsistentNaming
        public int IOErrorCount
        {
            get { return _ioErrorCount; }
        }

        /// <summary>
        /// Gets the clustermap rev# of the <see cref="Server" />.
        /// </summary>
        /// <value>
        /// The revision.
        /// </value>
        public uint Revision
        {
            get { return _bucketConfig.Rev; }
        }

        /// <summary>
        /// Handles the Elapsed event of the _heartBeatTimer control which is enabled
        /// whenever a node is unresponsive and possible offline. Once it is started,
        /// the node will be flagged as <see cref="_isDown"/> (which will be true). When the node
        /// is down, all operations (K/V, view and or query) that are mapped to this node will fail
        /// with a <see cref="NodeUnavailableException"/> - however, since operations are retried,
        /// it may be routed to a live node and succeed. The logs will reflect this but the result
        /// to the user will be a successful execution of a given operation.
        /// </summary>
        private void _heartBeatTimer_Elapsed(object state)
        {
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "HB-" + Thread.CurrentThread.ManagedThreadId;
            }
            Log.Info("Checking if node {0} is down: {1}", EndPoint, _isDown);
            if (_isDown && !_disposed)
            {
                if (IsDataNode)
                {
                    CheckDataNode();
                }
            }
        }

        /// <summary>
        /// If a data only node is flaged as down, this method will be invoked every <see cref="ClientConfiguration.NodeAvailableCheckInterval"/>.
        /// When invoked, it will attempt to get a connection and perform a NOOP on it.
        /// If the NOOP succeeds, then the node will be put back into rotation.
        /// </summary>
        void CheckDataNode()
        {
            IConnection connection = null;
            try
            {
                //if we have a sasl mechanism, we just try a noop
                connection = _ioService.ConnectionPool.Acquire();
                var noop = new Noop(new DefaultTranscoder(), 1000);

                var result = _ioService.Execute(noop);
                if (result.Success)
                {
                    Log.Info("Successfully connected and marking data node {0} as up.", EndPoint);
                    _isDown = false;
                }
                else
                {
                    Log.Info("The data node {0} is still down: {1}", EndPoint, result.Status);
                }
            }
                // ReSharper disable once CatchAllClause
            catch (Exception e)
            {
                // ReSharper disable once HeapView.ObjectAllocation
                Log.Info("The data node {0} is still down: {1}", EndPoint, e.Message);
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
                        StartHeartbeatTimer();
                    }
                    _ioService.ConnectionPool.Release(connection);
                }
                else
                {
                    StartHeartbeatTimer();
                }
            }
        }

        /// <summary>
        /// This method checks to see if the node has experianced a number of IO failures which exceed
        /// the <see cref="ClientConfiguration.IOErrorThreshold"/> value defined in the configuration
        /// within a specific duration specified by <see cref="ClientConfiguration.IOErrorCheckInterval"/>.
        /// </summary>
        /// <param name="isDead">if set to <c>true</c> is dead.</param>
        public void CheckOnline(bool isDead)
        {
            if (isDead && IsDataNode)
            {
                try
                {
                    _resetEvent.WaitOne();

                    var current = DateTime.Now;
                    var last = _lastIOErrorCheckedTime.AddMilliseconds(_clientConfiguration.IOErrorCheckInterval);
                    Interlocked.Increment(ref _ioErrorCount);

                    Log.Info("Checking if node {0} should be down - last: {1}, current: {2}, count: {3}",
                        EndPoint, last.TimeOfDay, current.TimeOfDay, _ioErrorCount);

                    if (_ioErrorCount > _clientConfiguration.IOErrorThreshold)
                    {
                        if (last < current)
                        {
                            Log.Info("Marking node {0} as down - last: {1}, current: {2}, count: {3}",
                                EndPoint, last.TimeOfDay, current.TimeOfDay, _ioErrorCount);

                            _isDown = true;
                            StartHeartbeatTimer();
                        }
                        Interlocked.Exchange(ref _ioErrorCount, 0);
                        _lastIOErrorCheckedTime = DateTime.Now;
                    }
                }
                finally
                {
                    _resetEvent.Set();
                }
            }
        }

        /// <summary>
        /// If the node is down, handles the return result for a K/V operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        private IOperationResult HandleNodeUnavailable(IOperation operation)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            operation.Exception = new NodeUnavailableException(msg);
            operation.HandleClientError(msg, ResponseStatus.NodeUnavailable);
            return  operation.GetResult();
        }

        /// <summary>
        /// If the node is down, handles the return result for a K/V operation.
        /// </summary>
        /// <typeparam name="T">The message body <see cref="Type"/>.</typeparam>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        private IOperationResult<T> HandleNodeUnavailable<T>(IOperation<T> operation)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            operation.Exception = new NodeUnavailableException(msg);
            operation.HandleClientError(msg, ResponseStatus.NodeUnavailable);
            return operation.GetResultWithValue();
        }

        /// <summary>
        /// If the node is down, handles the return result for a query operation.
        /// </summary>
        /// <typeparam name="T">The message body <see cref="Type"/>.</typeparam>
        /// <param name="query">The query.</param>
        /// <returns></returns>
        private IQueryResult<T> HandleNodeUnavailable<T>(IQueryRequest query)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            return new QueryResult<T>
            {
                Exception = new NodeUnavailableException(msg),
                Success = false,
                Status = QueryStatus.Fatal
            };
        }

        private ISearchQueryResult HandleNodeUnavailable(IFtsQuery query)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

            return new SearchQueryResult
            {
                Exception = new NodeUnavailableException(msg),
                Success = false,
                Status = SearchStatus.Failed
            };
        }

        /// <summary>
        /// Creates a failure result for an <see cref="IAnalyticsResult{T}"/> when a node is not available.
        /// </summary>
        /// <typeparam name="T">The target type for result rows to deserialize into.</typeparam>
        /// <param name="request">The analytics request.</param>
        /// <returns></returns>
        private IAnalyticsResult<T> HandleNodeUnavailable<T>(IAnalyticsRequest request)
        {
            var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint, _clientConfiguration.NodeAvailableCheckInterval);

            return new AnalyticsResult<T>
            {
                Exception = new NodeUnavailableException(msg),
                Success = false,
                Status = QueryStatus.Fatal
            };
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
            operation.CurrentHost = EndPoint;
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
                    Log.Debug("Sending {0} with key {1} using server {2}", operation.GetType().Name, operation.Key, EndPoint);
                    result = _ioService.Execute(operation);
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
            operation.CurrentHost = EndPoint;
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
                    Log.Debug("Sending {0} with key {1} using server {2}", operation.GetType().Name, User(operation.Key), EndPoint);
                    result = _ioService.Execute(operation);
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
            operation.CurrentHost = EndPoint;
            if (_isDown)
            {
                var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

                await operation.Completed(new SocketAsyncState
                {
                    Exception = new NodeUnavailableException(msg),
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.NodeUnavailable
                }).ContinueOnAnyContext();
                return;
            }
            await _ioService.ExecuteAsync(operation).ContinueOnAnyContext();
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
            operation.CurrentHost = EndPoint;
            if (_isDown)
            {
                var msg = ExceptionUtil.GetNodeUnavailableMsg(EndPoint,
                    _clientConfiguration.NodeAvailableCheckInterval);

                await operation.Completed(new SocketAsyncState
                {
                    Exception = new NodeUnavailableException(msg),
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.NodeUnavailable
                }).ContinueOnAnyContext();
                return;
            }
            await _ioService.ExecuteAsync(operation).ContinueOnAnyContext();
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
                result = query.IsStreaming
                    ? _streamingViewClient.ExecuteAsync<T>(query)
                    : ViewClient.ExecuteAsync<T>(query);
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
                result = query.IsStreaming
                    ? _streamingViewClient.Execute<T>(query)
                    : ViewClient.Execute<T>(query);
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
            if (_isDown)
            {
                result = HandleNodeUnavailable<T>(queryRequest);
            }
            else
            {
                try
                {
                    queryRequest.BaseUri(CachedQueryBaseUri);
                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (queryRequest.IsStreaming)
                    {
                        result = _streamingQueryClient.Query<T>(queryRequest);
                    }
                    else
                    {
                        result = QueryClient.Query<T>(queryRequest);
                    }
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
            return ((IServer)this).SendAsync<T>(queryRequest, CancellationToken.None);
        }

        /// <summary>
        /// Sends a request for a N1QL query to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest" /> object.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns></returns>
        async Task<IQueryResult<T>> IServer.SendAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            IQueryResult<T> result;
            if (_isDown)
            {
                result = HandleNodeUnavailable<T>(queryRequest);
            }
            else
            {
                try
                {
                    queryRequest.BaseUri(CachedQueryBaseUri);
                    if (queryRequest.IsStreaming)
                    {
                        result = await _streamingQueryClient.QueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
                    }
                    else
                    {
                        result = await QueryClient.QueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
                    }
                }
                catch (Exception e)
                {
                    result = new QueryResult<T>
                    {
                        Exception = e,
                        Message = e.Message,
                        Success = false
                    };
                }
            }
            return result;
        }

        public async Task<ISearchQueryResult> SendAsync(SearchQuery searchQuery)
        {
            ISearchQueryResult searchResult;
            if (_isDown)
            {
                searchResult = HandleNodeUnavailable(searchQuery.Query);
            }
            else
            {
                try
                {
                    searchResult = await SearchClient.QueryAsync(searchQuery).ContinueOnAnyContext();
                }
                catch (Exception e)
                {
                    searchResult = new SearchQueryResult
                    {
                        Exception = e,
                        Success = false
                    };
                }
            }
            return searchResult;
        }

        public ISearchQueryResult Send(SearchQuery searchQuery)
        {
            ISearchQueryResult searchResult;
            if (_isDown)
            {
                searchResult = HandleNodeUnavailable(searchQuery.Query);
            }
            else
            {
                try
                {
                    searchResult = SearchClient.Query(searchQuery);
                }
                catch (Exception e)
                {
                    searchResult = new SearchQueryResult
                    {
                        Exception = e,
                        Success = false
                    };
                }
            }
            return searchResult;
        }

        /// <summary>
        /// Sends an analytics request to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="analyticsRequest">The analytics request.</param>
        /// <returns></returns>
        public IAnalyticsResult<T> Send<T>(IAnalyticsRequest analyticsRequest)
        {
            IAnalyticsResult<T> result;
            if (_isDown)
            {
                result = HandleNodeUnavailable<T>(analyticsRequest);
            }
            else
            {
                try
                {
                    result = AnalyticsClient.Query<T>(analyticsRequest);
                }
                catch (Exception exception)
                {
                    result = new AnalyticsResult<T>
                    {
                        Exception = exception,
                        Message = exception.Message,
                        Success = false,
                    };
                }
            }
            return result;
        }

        /// <summary>
        /// Asynchronously sends an analytics request to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="analyticsRequest">The analytics request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<IAnalyticsResult<T>> SendAsync<T>(IAnalyticsRequest analyticsRequest, CancellationToken cancellationToken)
        {
            Task<IAnalyticsResult<T>> result;
            if (_isDown)
            {
                result = Task.FromResult(HandleNodeUnavailable<T>(analyticsRequest));
            }
            else
            {
                try
                {
                    result = AnalyticsClient.QueryAsync<T>(analyticsRequest, cancellationToken);
                }
                catch (Exception exception)
                {
                    result = Task.FromResult<IAnalyticsResult<T>>(new AnalyticsResult<T>
                    {
                        Exception = exception,
                        Message = exception.Message,
                        Success = false,
                    });
                }
            }
            return result;
        }

        public void MarkDead()
        {
            IsDown = true;
            if (!_disposed && _heartBeatTimer != null)
            {
                try
                {
                    StartHeartbeatTimer();
                }
                catch (ObjectDisposedException)
                {
                    //another thread has already disposed or finalized on this object
                }
            }
        }

        public void Dispose()
        {
            Log.Info("Disposing Server for {0}", EndPoint);
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

        private void StartHeartbeatTimer()
        {
            try
            {
                _heartBeatTimer.Change((int) _clientConfiguration.NodeAvailableCheckInterval, Timeout.Infinite);
            }
            catch (ObjectDisposedException e)
            {
                //this will happen in debug mode and can be ignored
                Log.Debug(e);
            }
        }

        private void Dispose(bool disposing)
        {
            lock (_syncObj)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    IsDown = true;
                    if (disposing)
                    {
                        GC.SuppressFinalize(this);
                    }

                    if (_heartBeatTimer != null)
                    {
                        _heartBeatTimer.Dispose();
                    }
                    if (_ioService != null)
                    {
                        _ioService.Dispose();
                    }
                }
            }
        }

#if DEBUG
        ~Server()
        {
            Log.Debug("Finalizing Server for {0}", EndPoint);
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
