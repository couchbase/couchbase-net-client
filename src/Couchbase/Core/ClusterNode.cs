using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.Logging;
using Couchbase.Core.Utils;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Couchbase.Core
{
    internal partial class ClusterNode : IClusterNode, IConnectionInitializer, IEquatable<ClusterNode>
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly ClusterContext _context;
        private readonly ILogger<ClusterNode> _logger;
        private readonly TypedRedactor _redactor;
        private readonly IRequestTracer _tracer;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ObjectPool<OperationBuilder> _operationBuilderPool;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private Uri _queryUri;
        private Uri _analyticsUri;
        private Uri _searchUri;
        private Uri _viewsUri;
        private Uri _eventingUri;
        private NodeAdapter _nodesAdapter;
        private readonly ObservableCollection<HostEndpointWithPort> _keyEndPoints = new();
        private readonly string _cachedToString;
        private volatile bool _disposed;
        private readonly string _localHostName;
        private readonly string _remoteHostName;

        public ClusterNode(ClusterContext context, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger,
            ObjectPool<OperationBuilder> operationBuilderPool, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory,
            TypedRedactor redactor, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, IRequestTracer tracer)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _operationBuilderPool =
                operationBuilderPool ?? throw new ArgumentNullException(nameof(operationBuilderPool));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentException(nameof(saslMechanismFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer;
            EndPoint = endPoint;

            try
            {
                _localHostName = Dns.GetHostName();
            }
            catch (Exception e)
            {
                LogCannotFetchHostName(e);
            }

            _cachedToString = $"{EndPoint}-{_id}";
            _remoteHostName = EndPoint.ToString();

            KeyEndPoints = new ReadOnlyObservableCollection<HostEndpointWithPort>(_keyEndPoints);
            UpdateKeyEndPoints();
            ((INotifyCollectionChanged)_keyEndPoints).CollectionChanged += (_, e) => OnKeyEndPointsChanged(e);

            if (connectionPoolFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionPoolFactory));
            }

            ConnectionPool = connectionPoolFactory.Create(this);

            if (nodeAdapter != null)
            {
                NodesAdapter = nodeAdapter;
            }
        }

        public ClusterNode(ClusterContext context)
            : this(context, context.ServiceProvider.GetRequiredService<ObjectPool<OperationBuilder>>(),
                context.ServiceProvider.GetRequiredService<CircuitBreaker>())
        {
        }

        public ClusterNode(ClusterContext context, ObjectPool<OperationBuilder> operationBuilderPool, ICircuitBreaker circuitBreaker)
        {
            _context = context;
            _operationBuilderPool = operationBuilderPool;
            _circuitBreaker = circuitBreaker;
        }

        public bool IsAssigned => Owner != null;
        public IBucket Owner { get; set; }

        public NodeAdapter NodesAdapter
        {
            get => _nodesAdapter;
            set
            {
                _nodesAdapter = value;
                BuildServiceUris();
                UpdateKeyEndPoints();
            }
        }

        public HostEndpointWithPort EndPoint { get; }
        public BucketType BucketType { get; internal set; }

        /// <inheritdoc />
        public IReadOnlyCollection<HostEndpointWithPort> KeyEndPoints { get; }

        public Uri EventingUri
        {
            get
            {
                LastEventingActivity = DateTime.UtcNow;
                return _eventingUri;
            }
            set => _eventingUri = value;
        }

        public Uri QueryUri
        {
            get
            {
                LastQueryActivity = DateTime.UtcNow;
                return _queryUri;
            }
            set => _queryUri = value;
        }

        public Uri AnalyticsUri
        {
            get
            {
                LastAnalyticsActivity = DateTime.UtcNow;
                return _analyticsUri;
            }
            set => _analyticsUri = value;
        }

        public Uri SearchUri
        {
            get
            {
                LastSearchActivity = DateTime.UtcNow;
                return _searchUri;
            }
            set => _searchUri = value;
        }

        public Uri ViewsUri
        {
            get
            {
                LastViewActivity = DateTime.UtcNow;
                return _viewsUri;
            }
            set => _viewsUri = value;
        }

        public Uri ManagementUri { get; set; }
        public ErrorMap ErrorMap { get; set; }
        public ServerFeatureSet ServerFeatures { get; private set; }
        public IConnectionPool ConnectionPool { get; }
        public List<Exception> Exceptions { get; set; }//TODO catch and hold until first operation per RFC
        public bool HasViews => NodesAdapter?.IsViewNode ?? false;
        public bool HasAnalytics => NodesAdapter?.IsAnalyticsNode ?? false;
        public bool HasQuery => NodesAdapter?.IsQueryNode ?? false;
        public bool HasSearch => NodesAdapter?.IsSearchNode ?? false;
        public bool HasKv => NodesAdapter?.IsKvNode ?? false;
        public bool HasEventing => NodesAdapter?.IsEventingNode ?? false;
        public DateTime? LastViewActivity { get; private set; }
        public DateTime? LastQueryActivity { get; private set; }
        public DateTime? LastSearchActivity { get; private set; }
        public DateTime? LastAnalyticsActivity { get; private set; }
        public DateTime? LastKvActivity { get; private set; }
        public DateTime? LastEventingActivity { get; private set; }

        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler KeyEndPointsChanged;

        private void OnKeyEndPointsChanged(NotifyCollectionChangedEventArgs e)
        {
            KeyEndPointsChanged?.Invoke(this, e);
        }

        public async Task InitializeAsync()
        {
            //If its the bootstrap node the node adapter cannot be applied until after fetching the config
            if (NodesAdapter == null || NodesAdapter.IsKvNode)
            {
                await ConnectionPool.InitializeAsync(_context.CancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<ErrorMap> GetErrorMap(IConnection connection, IRequestSpan span, CancellationToken cancellationToken = default)
        {
            using var childSpan = span.ChildSpan(OuterRequestSpans.ServiceSpan.Internal.GetErrorMap);
            using var errorMapOp = new GetErrorMap
            {
                Transcoder = _context.GlobalTranscoder,
                OperationBuilderPool = _operationBuilderPool,
                Opaque = SequenceGenerator.GetNext(),
                Span = childSpan
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout, cancellationToken);
            try
            {
                await ExecuteOp(connection, errorMapOp, ctp.TokenPair).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Check to see if it was because of a "hung" socket which causes the token to timeout
                if (ctp.IsInternalCancellation)
                {
                    ThrowHelper.ThrowTimeoutException(errorMapOp);
                }
                throw;
            }
            return new ErrorMap(errorMapOp.GetValue());
        }

        public async Task HelloHello()
        {
            foreach (var connection in ConnectionPool.GetConnections())
            {
                _logger.LogDebug("Starting connection reinitialization on server {endpoint}.", EndPoint);
                using var rootSpan = RootSpan("reinitialize_connection");

                var serverFeatureList = await Hello(connection, rootSpan).ConfigureAwait(false);
                connection.ServerFeatures = serverFeatureList != null
                    ? new ServerFeatureSet(serverFeatureList)
                    : ServerFeatureSet.Empty;
                ServerFeatures = connection.ServerFeatures;
            }
        }

        private async Task<ServerFeatures[]> Hello(IConnection connection, IRequestSpan span, CancellationToken cancellationToken = default)
        {
            var features = new List<ServerFeatures>
            {
                IO.Operations.ServerFeatures.SelectBucket,
                IO.Operations.ServerFeatures.AlternateRequestSupport,
                IO.Operations.ServerFeatures.SynchronousReplication,
                IO.Operations.ServerFeatures.SubdocXAttributes,
                IO.Operations.ServerFeatures.XError,
                IO.Operations.ServerFeatures.PreserveTtl
            };

            if (Owner != null && Owner.SupportsCollections)
            {
                features.Add(IO.Operations.ServerFeatures.Collections);
            }

            if (_context.ClusterOptions.EnableMutationTokens)
            {
                features.Add(IO.Operations.ServerFeatures.MutationSeqno);
            }

            if (_context.ClusterOptions.EnableOperationDurationTracing)
            {
                features.Add(IO.Operations.ServerFeatures.ServerDuration);
            }

            if (_context.ClusterOptions.UnorderedExecutionEnabled)
            {
                features.Add(IO.Operations.ServerFeatures.UnorderedExecution);
            }

            if (_context.ClusterOptions.Compression)
            {
                switch (_context.ServiceProvider.GetRequiredService<ICompressionAlgorithm>().Algorithm)
                {
                    case CompressionAlgorithm.Snappy:
                        features.Add(IO.Operations.ServerFeatures.SnappyCompression);
                        break;
                }
            }

            using var childSpan = span.ChildSpan(OuterRequestSpans.ServiceSpan.Internal.Hello);
            using var heloOp = new Hello
            {
                Key = Core.IO.Operations.Hello.BuildHelloKey(connection.ConnectionId),
                Content = features.ToArray(),
                Transcoder = _context.GlobalTranscoder,
                OperationBuilderPool = _operationBuilderPool,
                Opaque = SequenceGenerator.GetNext(),
                Span = childSpan,
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout, cancellationToken);
            try
            {
                await ExecuteOp(connection, heloOp, ctp.TokenPair).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Check to see if it was because of a "hung" socket which causes the token to timeout
                if (ctp.IsInternalCancellation)
                {
                    ThrowHelper.ThrowTimeoutException(heloOp);
                }
                throw;
            }
            return heloOp.GetValue();
        }

        public async Task<Manifest> GetManifest()
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.GetManifest);
            using var manifestOp = new GetManifest
            {
                Transcoder = _context.GlobalTranscoder,
                OperationBuilderPool = _operationBuilderPool,
                Opaque = SequenceGenerator.GetNext(),
                Span = rootSpan,
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout);
            try
            {
                await ExecuteOp(ConnectionPool, manifestOp, ctp.TokenPair).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Check to see if it was because of a "hung" socket which causes the token to timeout
                if (ctp.IsInternalCancellation)
                {
                    ThrowHelper.ThrowTimeoutException(manifestOp);
                }
                throw;
            }
            return manifestOp.GetValue();
        }

        public async Task SelectBucketAsync(string bucketName, CancellationToken cancellationToken = default)
        {
            await ConnectionPool.SelectBucketAsync(bucketName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<BucketConfig> GetClusterMap()
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.GetClusterMap);
            using var configOp = new Config
            {
                Transcoder = _context.GlobalTranscoder,
                OperationBuilderPool = _operationBuilderPool,
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = EndPoint,
                Span = rootSpan,
                Timeout = _context.ClusterOptions.KvTimeout
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout);
            try
            {
                await ExecuteOp(ConnectionPool, configOp, ctp.TokenPair).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Check to see if it was because of a "hung" socket which causes the token to timeout
                if (ctp.IsInternalCancellation)
                {
                    ThrowHelper.ThrowTimeoutException(configOp);
                }
                throw;
            }
            var config = configOp.GetValue();

            if (config != null)
            {
                config.ReplacePlaceholderWithBootstrapHost(EndPoint.Host);
            }

            return config;
        }

        private void BuildServiceUris()
        {
            QueryUri = NodesAdapter?.GetQueryUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger);
            SearchUri = NodesAdapter?.GetSearchUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger);
            AnalyticsUri = NodesAdapter?.GetAnalyticsUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger);
            ViewsUri = NodesAdapter?.GetViewsUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger); //TODO move to IBucket level?
            ManagementUri = NodesAdapter?.GetManagementUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger);
            EventingUri = NodesAdapter?.GetEventingUri(_context.ClusterOptions).SetServicePointOptions(_context.ClusterOptions, _logger);
        }

        /// <summary>
        /// Ensures that <see cref="KeyEndPoints"/> is correct, given the values of
        /// <see cref="EndPoint"/> and <see cref="NodesAdapter"/>.
        /// </summary>
        private void UpdateKeyEndPoints()
        {
            if (_nodesAdapter == null)
            {
                // NodesAdapter has not been set, so KeyEndPoints should be the EndPoint property

                if (_keyEndPoints.Count > 0)
                {
                    foreach (var endPoint in _keyEndPoints.Where(p => !p.Equals(EndPoint)).ToList())
                    {
                        _keyEndPoints.Remove(endPoint);
                    }
                }

                if (_keyEndPoints.Count == 0)
                {
                    _keyEndPoints.Add(EndPoint);
                }
            }
            else
            {
                // KeyEndPoints should be the K/V and K/V SSL ports at EndPoint.Address. Remove others add if necessary.

                var kvEndpoint = new HostEndpointWithPort(EndPoint.Host, _nodesAdapter.KeyValue);
                var sslEndpoint = new HostEndpointWithPort(EndPoint.Host, _nodesAdapter.KeyValueSsl);

                if (_keyEndPoints.Count > 0)
                {
                    foreach (var endPoint in _keyEndPoints.Where(p => !p.Equals(kvEndpoint) && !p.Equals(sslEndpoint)).ToList())
                    {
                        _keyEndPoints.Remove(endPoint);
                    }
                }

                if (!_keyEndPoints.Contains(kvEndpoint))
                {
                    _keyEndPoints.Add(kvEndpoint);
                }

                if (sslEndpoint.Port > 0 && !_keyEndPoints.Contains(sslEndpoint))
                {
                    _keyEndPoints.Add(sslEndpoint);
                }
            }
        }

        public Task SendAsync(IOperation op, CancellationTokenPair tokenPair = default)
        {
            LogCircuitBreakerState(_circuitBreaker.State);

            // This avoids an extra async/await method if circuit breakers are disabled.
            return _circuitBreaker.Enabled
                ? SendAsyncWithCircuitBreaker(op, tokenPair)
                : ExecuteOp(ConnectionPool, op, tokenPair);
        }

        private async Task SendAsyncWithCircuitBreaker(IOperation op, CancellationTokenPair tokenPair = default)
        {
            if (_circuitBreaker.AllowsRequest())
            {
                _circuitBreaker.Track();

                try
                {
                    await ExecuteOp(ConnectionPool, op, tokenPair).ConfigureAwait(false);

                    UpdateCircuitBreaker(op, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Timeouts will be Ambiguous or UnambiguousTimeoutException, but OperationCanceledException
                    // indicates an external cancellation which shouldn't be treated as a success or a failure.
                    UpdateCircuitBreaker(op, ex);
                    throw;
                }
            }
            else
            {
                if (_circuitBreaker.State == CircuitBreakerState.HalfOpen)
                {
                    QueueHalfOpenCircuitBreakerTest(op.Span);
                }

                throw new CircuitBreakerException();
            }
        }

        private async Task ExecuteOp(Func<IOperation, object, CancellationToken, Task> sender, IOperation op, object state, CancellationTokenPair tokenPair = default)
        {
            LogKvExecutingOperation(op.OpCode, _redactor.SystemData(EndPoint), _redactor.UserData(op.Key), op.Opaque);

            try
            {
                // Await the send in case the send throws an exception (i.e. SendQueueFullException)
                await sender(op, state, tokenPair).ConfigureAwait(false);

                ResponseStatus status;
                using (new OperationCancellationRegistration(op, tokenPair))
                {
                    status = await op.Completed.ConfigureAwait(false);
                }

                if (status != ResponseStatus.Success)
                {
                    LogKvStatusReturned(_redactor.SystemData(EndPoint), status, op.OpCode, _redactor.UserData(op.Key), op.Opaque);

                    if (status == ResponseStatus.TransportFailure && op is Hello && ErrorMap == null)
                    {
                        throw new ConnectException(
                            "General network failure - Check server ports and cluster encryption setting.");
                    }

                    if (status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        var config = op.ReadConfig(_context.GlobalTranscoder);
                        _context.PublishConfig(config);
                    }

                    //sub-doc path failures for lookups are handled when the ContentAs() method is called.
                    //so we simply return back to the caller and let it be handled later.
                    if (status == ResponseStatus.SubDocMultiPathFailure && op.OpCode == OpCode.MultiLookup)
                    {
                        return;
                    }

                    // The sub-doc operation was a success, but the doc remains deleted/tombstone.
                    if (status == ResponseStatus.SubDocSuccessDeletedDocument
                        || status == ResponseStatus.SubdocMultiPathFailureDeleted)
                    {
                        return;
                    }

                    var code = (short) status;
                    if (!ErrorMap.TryGetGetErrorCode(code, out var errorCode))
                    {
                        //We can ignore transport exceptions here as they are generated internally in cases a KV cannot be completed.
                        if (code != 0x0500)
                        {
                            LogKvStatusNotFound(code);
                        }
                    }

                    //Likely an "orphaned operation"
                    if (status == ResponseStatus.TransportFailure || status == ResponseStatus.OperationTimeout)
                    {
                        //log as orphan if internal criteria met
                        op.LogOrphaned();
                    }

                    //Contextual error information
                    var ctx = new KeyValueErrorContext
                    {
                        BucketName = Owner?.Name,
                        ClientContextId = op.Opaque.ToStringInvariant(),
                        DocumentKey = op.Key,
                        Cas = op.Cas,
                        CollectionName = op.CName,
                        ScopeName = op.SName,
                        Message = errorCode?.ToString(),
                        Status = status,
                        OpCode = op.OpCode,
                        DispatchedFrom = _localHostName,
                        DispatchedTo = _remoteHostName,
                        RetryReasons = op.RetryReasons
                    };

                    if (op.Resilient)
                    {
                        var exception = status.CreateException(ctx, op);
                        op.TrySetException(exception);
                        return;
                    }

                    throw status.CreateException(ctx, op);
                }

                LogKvOperationCompleted(op.OpCode, _redactor.SystemData(EndPoint), _redactor.UserData(op.Key), op.Opaque);
            }
            catch (OperationCanceledException)
            {
                // Timeout handling logic is also in RetryOrchestrator, however this method can also be reached without
                // passing through RetryOrchestrator for cases like diagnostics or bootstrapping. Therefore, we need the logic
                // in both places.

                //log as orphan if internal criteria met
                op.LogOrphaned();

                if (!tokenPair.IsExternalCancellation)
                {
                    LogKvOperationTimeout(_redactor.SystemData(EndPoint), op.OpCode, _redactor.UserData(op.Key), op.Opaque, op.IsSent);

                    // If this wasn't an externally requested cancellation, it's a timeout, so convert to a TimeoutException
                    ThrowHelper.ThrowTimeoutException(op, new KeyValueErrorContext
                    {
                        BucketName = Owner?.Name,
                        ClientContextId = op.Opaque.ToStringInvariant(),
                        DocumentKey = op.Key,
                        Cas = op.Cas,
                        CollectionName = op.CName,
                        ScopeName = op.SName,
                        OpCode = op.OpCode,
                        DispatchedFrom = op.LastDispatchedFrom,
                        DispatchedTo = op.LastDispatchedTo,
                        RetryReasons = op.RetryReasons
                    });
                }

                throw;
            }
            catch (Exception e)
            {
                LogKvOperationFailed(e, op);

                throw;
            }
        }

        public Task ExecuteOp(IOperation op, CancellationTokenPair tokenPair = default)
        {
            return ExecuteOp(ConnectionPool, op, tokenPair);
        }

        private Task ExecuteOp(IConnectionPool connectionPool, IOperation op, CancellationTokenPair tokenPair = default)
        {
            // op and connectionPool come back via lambda parameters to prevent an extra closure heap allocation
            return ExecuteOp((op2, state, effectiveToken) => ((IConnectionPool)state).SendAsync(op2, effectiveToken),
                op, connectionPool, tokenPair);
        }

        public Task ExecuteOp(IConnection connection, IOperation op, CancellationTokenPair tokenPair = default)
        {
            // op and connection come back via lambda parameters to prevent an extra closure heap allocation
            return ExecuteOp((op2, state, effectiveToken) => op2.SendAsync((IConnection)state, effectiveToken),
                op, connection, tokenPair);
        }

        #region IConnectionInitializer

        async Task IConnectionInitializer.InitializeConnectionAsync(IConnection connection, CancellationToken cancellationToken)
        {
            if (!_disposed)
            {
                LogConnectionInitialization(_redactor.SystemData(EndPoint));
                using var rootSpan = RootSpan("initialize_connection");

                using (var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout, cancellationToken))
                {
                    var serverFeatureList = await Hello(connection, rootSpan, ctp.TokenPair).ConfigureAwait(false);
                    connection.ServerFeatures = serverFeatureList != null
                        ? new ServerFeatureSet(serverFeatureList)
                        : ServerFeatureSet.Empty;
                    ServerFeatures = connection.ServerFeatures;
                }

                using (var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout, cancellationToken))
                {
                    ErrorMap = await GetErrorMap(connection, rootSpan, ctp.TokenPair).ConfigureAwait(false);
                }

                var mechanismType = _context.ClusterOptions.EffectiveEnableTls
                    ? MechanismType.Plain
                    : MechanismType.ScramSha1;
                var saslMechanism = _saslMechanismFactory.Create(mechanismType, _context.ClusterOptions.UserName,
                    _context.ClusterOptions.Password);

                await saslMechanism.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
                rootSpan.Dispose();
            }
        }

        async Task IConnectionInitializer.SelectBucketAsync(IConnection connection, string bucketName, CancellationToken cancellationToken)
        {
            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.SelectBucket);
                using var selectBucketOp = new SelectBucket
                {
                    Transcoder = _context.GlobalTranscoder,
                    OperationBuilderPool = _operationBuilderPool,
                    Key = bucketName,
                    Span = rootSpan,
                };

                using var ctp = CancellationTokenPairSource.FromTimeout(_context.ClusterOptions.KvTimeout, cancellationToken);
                try
                {
                    await ExecuteOp(connection, selectBucketOp, ctp.TokenPair).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //Check to see if it was because of a "hung" socket which causes the token to timeout
                    if (ctp.IsInternalCancellation)
                    {
                        ThrowHelper.ThrowTimeoutException(selectBucketOp);
                    }
                    throw;
                }
            }
            catch (DocumentNotFoundException)
            {
                // If DNF exception then BucketNotConnected was returned so close the connection and let it get cleaned up later
                // Use the synchronous dispose as we don't need to wait for in-flight operations to complete.
                connection.Dispose();

                LogCouldNotSelectBucket(_redactor.MetaData(bucketName));
            }
        }

        #endregion

        #region Circuit Breaker

        #nullable enable

        /// <summary>
        /// Updates the circuit breaker status. Null exception indicates success.
        /// </summary>
        private void UpdateCircuitBreaker(IOperation op, Exception? exception)
        {
            if (exception is null)
            {
                _circuitBreaker.MarkSuccess();
            }
            else
            {
                if (_circuitBreaker.CompletionCallback(exception))
                {
                    LogCircuitBreakerMarkFailure(op.Opaque, _redactor.SystemData(ConnectionPool.EndPoint));

                    _circuitBreaker.MarkFailure();
                }
            }
        }

        #nullable restore

        private void QueueHalfOpenCircuitBreakerTest(IRequestSpan parentSpan)
        {
            // Run half-open test in a separate thread so that the test doesn't delay
            // the circuit open exception from reaching the caller. UnsafeQueueUserWorkItem is
            // used to avoid capturing the ExecutionContext, which could cause side effects.
            // It also ensures that the initial work to send the half-open test, such as
            // serialization, etc, is pushed off to a different thread.

#if NETCOREAPP3_1_OR_GREATER
            ThreadPool.UnsafeQueueUserWorkItem(state =>
                {
                    var _ = HalfOpenCircuitBreakerTestAsync((IRequestSpan) state);
                },
                state: parentSpan,
                preferLocal: false);
#else
            ThreadPool.UnsafeQueueUserWorkItem(state =>
                {
                    var _ = HalfOpenCircuitBreakerTestAsync((IRequestSpan) state);
                },
                state: parentSpan);
#endif
        }

        private async Task HalfOpenCircuitBreakerTestAsync(IRequestSpan parentSpan)
        {
            try
            {
                LogCircuitBreakerSendingCanary(_redactor.SystemData(ConnectionPool.EndPoint));
                using (var ctp = CancellationTokenPairSource.FromTimeout(_circuitBreaker.CanaryTimeout))
                {
                    await ExecuteOp(ConnectionPool, new Noop
                    {
                        Transcoder = _context.GlobalTranscoder,
                        OperationBuilderPool = _operationBuilderPool,
                        Opaque = SequenceGenerator.GetNext(),
                        Span = parentSpan
                    }, ctp.TokenPair).ConfigureAwait(false);
                }

                _circuitBreaker.MarkSuccess();
            }
            catch (Exception e)
            {
                if (_circuitBreaker.CompletionCallback(e))
                {
                    LogCircuitBreakerCanaryFailed(e, _redactor.SystemData(ConnectionPool.EndPoint));
                    _circuitBreaker.MarkFailure();
                }
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            LogDispose(_redactor.SystemData(EndPoint));
            ConnectionPool?.Dispose();
        }

        #region Equality

        public bool Equals(ClusterNode other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Owner, other.Owner) && EndPoint.Equals(other.EndPoint);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClusterNode)obj);
        }

        public override int GetHashCode()
        {
#if NETSTANDARD2_0
            // Use ValueTuple to build the hash code from the components
            return (EndPoint, _id).GetHashCode();
#else
            return System.HashCode.Combine(EndPoint, _id);
#endif
        }
        #endregion

        #region ToString

        public override string ToString()
        {
            return _cachedToString;
        }

        #endregion

        #region Tracing

        private IRequestSpan RootSpan(string operation)
        {
            var span = _tracer.RequestSpan(operation);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }

        #endregion

        #region Logging

        [LoggerMessage(1, LogLevel.Debug, "Starting connection initialization on server {endpoint}.")]
        private partial void LogConnectionInitialization(Redacted<HostEndpointWithPort> endpoint);

        [LoggerMessage(2, LogLevel.Debug, "Disposing cluster node for {endpoint}")]
        private partial void LogDispose(Redacted<HostEndpointWithPort> endpoint);

        [LoggerMessage(3, LogLevel.Debug, "Cannot fetch hostname.")]
        private partial void LogCannotFetchHostName(Exception exception);

        [LoggerMessage(LoggingEvents.BootstrapEvent, LogLevel.Error, "The Bucket [{bucketName}] could not be selected. Either it does not exist, " +
                                                                     "is unavailable or the node itself does not have the Data service enabled.")]
        private partial void LogCouldNotSelectBucket(Redacted<string> bucketName);

        [LoggerMessage(100, LogLevel.Debug, "Executing op {opcode} on {endpoint} with key {key} and opaque {opaque}.")]
        private partial void LogKvExecutingOperation(OpCode opcode, Redacted<HostEndpointWithPort> endpoint, Redacted<string> key, uint opaque);

        [LoggerMessage(101, LogLevel.Debug, "Server {endpoint} returned {status} for op {opcode} with key {key} and opaque {opaque}.")]
        private partial void LogKvStatusReturned(Redacted<HostEndpointWithPort> endpoint, ResponseStatus status, OpCode opcode, Redacted<string> key, uint opaque);

        [LoggerMessage(102, LogLevel.Warning, "Unexpected Status for KeyValue operation not found in Error Map: 0x{code:X4}")]
        private partial void LogKvStatusNotFound(short code);

        [LoggerMessage(103, LogLevel.Debug, "Completed executing op {opCode} on {endpoint} with key {key} and opaque {opaque}")]
        private partial void LogKvOperationCompleted(OpCode opCode, Redacted<HostEndpointWithPort> endpoint, Redacted<string> key, uint opaque);

        [LoggerMessage(104, LogLevel.Debug, "Op failed: {op}")]
        private partial void LogKvOperationFailed(Exception ex, IOperation op);

        [LoggerMessage(105, LogLevel.Debug, "KV Operation timeout for op {opCode} on {endpoint} with key {key} and opaque {opaque}. Is orphaned: {isSent}")]
        private partial void LogKvOperationTimeout(Redacted<HostEndpointWithPort> endpoint, OpCode opCode, Redacted<string> key, uint opaque, bool isSent);

        [LoggerMessage(200, LogLevel.Debug, "CB: Current state is {state}.")]
        private partial void LogCircuitBreakerState(CircuitBreakerState state);

        [LoggerMessage(201, LogLevel.Debug, "CB: Marking a failure for {opaque} to {endpoint}.")]
        private partial void LogCircuitBreakerMarkFailure(uint opaque, Redacted<HostEndpointWithPort> endpoint);

        [LoggerMessage(202, LogLevel.Debug, "CB: Sending a canary to {endpoint}.")]
        private partial void LogCircuitBreakerSendingCanary(Redacted<HostEndpointWithPort> endpoint);

        [LoggerMessage(203, LogLevel.Debug, "CB: Marking a failure for canary sent to {endpoint}.")]
        private partial void LogCircuitBreakerCanaryFailed(Exception ex, Redacted<HostEndpointWithPort> endpoint);

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
