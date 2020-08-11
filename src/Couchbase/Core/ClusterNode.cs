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
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterNode : IClusterNode, IConnectionInitializer, IEquatable<ClusterNode>
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly ClusterContext _context;
        private readonly ILogger<ClusterNode> _logger;
        private readonly IRedactor _redactor;
        private readonly IRequestTracer _tracer;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ITypeTranscoder _transcoder;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private Uri _queryUri;
        private Uri _analyticsUri;
        private Uri _searchUri;
        private Uri _viewsUri;
        private NodeAdapter _nodesAdapter;
        private readonly ObservableCollection<IPEndPoint> _keyEndPoints = new ObservableCollection<IPEndPoint>();
        private readonly string _cachedToString;
        private volatile bool _disposed;

        public ClusterNode(ClusterContext context, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger, ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory, IRedactor redactor, IPEndPoint endPoint, BucketType bucketType, NodeAdapter nodeAdapter, IRequestTracer tracer)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentException(nameof(saslMechanismFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer;
            BucketType = bucketType;
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));

            _cachedToString = $"{EndPoint}-{_id}";

            KeyEndPoints = new ReadOnlyObservableCollection<IPEndPoint>(_keyEndPoints);
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
            : this(context, context.ServiceProvider.GetRequiredService<ITypeTranscoder>(),
                context.ServiceProvider.GetRequiredService<CircuitBreaker>())
        {
        }

        public ClusterNode(ClusterContext context, ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker)
        {
            _context = context;
            _transcoder = transcoder;
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

        public HostEndpoint BootstrapEndpoint { get; set; }
        public IPEndPoint EndPoint { get; }
        public BucketType BucketType { get; internal set; } = BucketType.Memcached;

        /// <inheritdoc />
        public IReadOnlyCollection<IPEndPoint> KeyEndPoints { get; }

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
        public short[] ServerFeatures { get; set; }
        public IConnectionPool ConnectionPool { get; }
        public List<Exception> Exceptions { get; set; }//TODO catch and hold until first operation per RFC
        public bool HasViews => NodesAdapter?.IsViewNode ?? false;
        public bool HasAnalytics => NodesAdapter?.IsAnalyticsNode ?? false;
        public bool HasQuery => NodesAdapter?.IsQueryNode ?? false;
        public bool HasSearch => NodesAdapter?.IsSearchNode ?? false;
        public bool HasKv => NodesAdapter?.IsKvNode ?? false;

        public bool Supports(ServerFeatures feature)
        {
            return ServerFeatures.Contains((short)feature);
        }

        public DateTime? LastViewActivity { get; private set; }
        public DateTime? LastQueryActivity { get; private set; }
        public DateTime? LastSearchActivity { get; private set; }
        public DateTime? LastAnalyticsActivity { get; private set; }
        public DateTime? LastKvActivity { get; private set; }

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

        private async Task<ErrorMap> GetErrorMap(IConnection connection, IInternalSpan span, CancellationToken cancellationToken = default)
        {
            using var childSpan = _tracer.InternalSpan(OperationNames.GetErrorMap, span);
            using var errorMapOp = new GetErrorMap
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Span = childSpan
            };
            await ExecuteOp(connection, errorMapOp, cancellationToken).ConfigureAwait(false);
            return errorMapOp.GetResultWithValue().Content;
        }

        private async Task<short[]> Hello(IConnection connection, IInternalSpan span, CancellationToken cancellationToken = default)
        {
            var features = new List<short>
            {
                (short) IO.Operations.ServerFeatures.SelectBucket,
                (short) IO.Operations.ServerFeatures.AlternateRequestSupport,
                (short) IO.Operations.ServerFeatures.SynchronousReplication,
                (short) IO.Operations.ServerFeatures.SubdocXAttributes,
                (short) IO.Operations.ServerFeatures.XError
            };

            if (BucketType != BucketType.Memcached)
            {
                features.Add((short) IO.Operations.ServerFeatures.Collections);
            }

            if (_context.ClusterOptions.EnableMutationTokens)
            {
                features.Add((short)IO.Operations.ServerFeatures.MutationSeqno);
            }

            if (_context.ClusterOptions.EnableOperationDurationTracing)
            {
                features.Add((short)IO.Operations.ServerFeatures.ServerDuration);
            }

            using var childSpan = _tracer.InternalSpan(OperationNames.Hello, span);
            using var heloOp = new Hello
            {
                Key = Core.IO.Operations.Hello.BuildHelloKey(connection.ConnectionId),
                Content = features.ToArray(),
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Span = childSpan,
            };

            await ExecuteOp(connection, heloOp, cancellationToken).ConfigureAwait(false);
            return heloOp.GetResultWithValue().Content;
        }

        public async Task<Manifest> GetManifest()
        {
            using var rootSpan = RootSpan(OperationNames.GetManifest);
            using var manifestOp = new GetManifest
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Span = rootSpan,
            };
            await ExecuteOp(ConnectionPool, manifestOp).ConfigureAwait(false);
            var manifestResult = manifestOp.GetResultWithValue();
            return manifestResult.Content;
        }

        public async Task SelectBucketAsync(IBucket bucket, CancellationToken cancellationToken = default)
        {
            await ConnectionPool.SelectBucketAsync(bucket.Name, cancellationToken).ConfigureAwait(false);

            Owner = bucket;
        }

        public async Task<BucketConfig> GetClusterMap()
        {
            using var rootSpan = RootSpan(OperationNames.GetClusterMap);
            using var configOp = new Config
            {
                CurrentHost = EndPoint,
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = EndPoint,
                Span = rootSpan,
            };
            await ExecuteOp(ConnectionPool, configOp).ConfigureAwait(false);

            var configResult = configOp.GetResultWithValue();
            var config = configResult.Content;

            if (config != null && EndPoint != null)
            {
                config.ReplacePlaceholderWithBootstrapHost(BootstrapEndpoint.Host);
            }

            return config;
        }

        public async Task<uint?> GetCid(string fullyQualifiedName)
        {
            using var rootSpan = RootSpan(OperationNames.GetCid);
            using var getCid = new GetCid
            {
                Key = fullyQualifiedName,
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Content = null,
                Span = rootSpan,
            };
            await ExecuteOp(ConnectionPool, getCid).ConfigureAwait(false);
            var resultWithValue = getCid.GetResultWithValue();
            return resultWithValue.Content;
        }

        private void BuildServiceUris()
        {
            QueryUri = NodesAdapter?.GetQueryUri(_context.ClusterOptions);
            SearchUri = NodesAdapter?.GetSearchUri(_context.ClusterOptions);
            AnalyticsUri = NodesAdapter?.GetAnalyticsUri(_context.ClusterOptions);
            ViewsUri = NodesAdapter?.GetViewsUri(_context.ClusterOptions); //TODO move to IBucket level?
            ManagementUri = NodesAdapter?.GetManagementUri(_context.ClusterOptions);
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

                var kvEndpoint = new IPEndPoint(EndPoint.Address, _nodesAdapter.KeyValue);
                var sslEndpoint = new IPEndPoint(EndPoint.Address, _nodesAdapter.KeyValueSsl);

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

                if (!_keyEndPoints.Contains(sslEndpoint))
                {
                    _keyEndPoints.Add(sslEndpoint);
                }
            }
        }

        public async Task SendAsync(IOperation op, CancellationToken token = default(CancellationToken), TimeSpan? timeout = null)
        {
            _logger.LogDebug("CB: Current state is {state}.", _circuitBreaker.State);

            if (_circuitBreaker.Enabled)
            {
                if (_circuitBreaker.AllowsRequest())
                {
                    _circuitBreaker.Track();
                    try
                    {

                        _logger.LogDebug("CB: Sending {opaque} to {endPoint}.", op.Opaque,
                            _redactor.SystemData(EndPoint));

                        await ExecuteOp(ConnectionPool, op, token).ConfigureAwait(false);
                        _circuitBreaker.MarkSuccess();
                    }
                    catch (Exception e)
                    {
                        if (_circuitBreaker.CompletionCallback(e))
                        {
                            _logger.LogDebug("CB: Marking a failure for {opaque} to {endPoint}.", op.Opaque,
                                _redactor.SystemData(ConnectionPool.EndPoint));

                            _circuitBreaker.MarkFailure();
                        }

                        throw;
                    }
                }
                else
                {
                    if (_circuitBreaker.State == CircuitBreakerState.HalfOpen)
                    {
                        try
                        {
                            _logger.LogDebug("CB: Sending a canary to {endPoint}.", _redactor.SystemData(ConnectionPool.EndPoint));
                            using (var cts = new CancellationTokenSource(_circuitBreaker.CanaryTimeout))
                            {
                                await ExecuteOp(ConnectionPool, new Noop() { Span = op.Span }, cts.Token).ConfigureAwait(false);
                            }

                            _circuitBreaker.MarkSuccess();
                        }
                        catch (Exception e)
                        {
                            if (_circuitBreaker.CompletionCallback(e))
                            {
                                _logger.LogDebug("CB: Marking a failure for canary sent to {endPoint}.", _redactor.SystemData(ConnectionPool.EndPoint));
                                _circuitBreaker.MarkFailure();
                            }
                        }
                    }

                    throw new CircuitBreakerException();
                }
            }
            else
            {
                await ExecuteOp(ConnectionPool, op, token).ConfigureAwait(false);
            }
        }

        private async Task ExecuteOp(Action<IOperation, object, CancellationToken> sender, IOperation op, object state, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            _logger.LogDebug("Executing op {opcode} on {endpoint} with key {key} and opaque {opaque}.", op.OpCode, EndPoint, _redactor.UserData(op.Key), op.Opaque);

            CancellationTokenSource cts = null;
            try
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(GetTimeout(timeout, op));
                token = cts.Token;

                sender(op, state, token);

                var status = await op.Completed.ConfigureAwait(false);
                if (status != ResponseStatus.Success)
                {
                    _logger.LogDebug("Server {endpoint} returned {status} for op {opcode} with key {key} and opaque {opaque}.",
                        EndPoint, status, op.OpCode, op.Key, op.Opaque);

                    if (status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        var config = op.GetConfig(_transcoder);
                        _context.PublishConfig(config);
                    }

                    //sub-doc path failures are handled when the ContentAs() method is called.
                    //so we simply return back to the caller and let it be handled later.
                    if (status == ResponseStatus.SubDocMultiPathFailure)
                    {
                        return;
                    }

                    var code = (short)status;
                    if (!ErrorMap.TryGetGetErrorCode(code, out var errorCode))
                    {
                        //We can ignore transport exceptions here as they are generated internally in cases a KV cannot be completed.
                        if (code != 0x0500)
                        {
                            _logger.LogWarning(
                                "Unexpected Status for KeyValue operation not found in Error Map: 0x{code}",
                                code.ToString("X4"));
                        }
                    }

                    //Contextual error information
                    var ctx = new KeyValueErrorContext
                    {
                        BucketName = Owner?.Name,
                        ClientContextId = op.Opaque.ToString(),
                        DocumentKey = op.Key,
                        Cas = op.Cas,
                        CollectionName = op.CName,
                        ScopeName = op.SName,
                        Message = errorCode?.ToString(),
                        Status = status
                    };
                    throw status.CreateException(ctx);
                }

                _logger.LogDebug("Completed executing op {opCode} on {endpoint} with key {key} and opaque {opaque}",
                    EndPoint, op.OpCode, _redactor.UserData(op.Key), op.Opaque);
            }
            catch (OperationCanceledException e)
            {
                _logger.LogDebug("KV Operation timeout for {key} on server {endpoint}.", op.Key, EndPoint);
                if (!e.CancellationToken.IsCancellationRequested)
                {
                    //oddly IsCancellationRequested is false when timed out
                    throw new TimeoutException();
                }

                throw;
            }
            finally
            {
                //clean up the token if we used a default token
                cts?.Dispose();
            }
        }

        public Task ExecuteOp(IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            return ExecuteOp(ConnectionPool, op, token);
        }

        private Task ExecuteOp(IConnectionPool connectionPool, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            // op and connectionPool come back via lambda parameters to prevent an extra closure heap allocation
            return ExecuteOp((op2, state, effectiveToken) => ((IConnectionPool)state).SendAsync(op2, effectiveToken),
                op, connectionPool, token);
        }

        public Task ExecuteOp(IConnection connection, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            // op and connection come back via lambda parameters to prevent an extra closure heap allocation
            return ExecuteOp((op2, state, effectiveToken) => op2.SendAsync((IConnection)state, effectiveToken),
                op, connection, token);
        }

        private TimeSpan GetTimeout(TimeSpan? optionsTimeout, IOperation op)
        {
            if (op.HasDurability)
            {
                op.Timeout = optionsTimeout ?? _context.ClusterOptions.KvDurabilityTimeout;
                return op.Timeout;
            }
            return optionsTimeout ?? _context.ClusterOptions.KvTimeout;
        }

        #region IConnectionInitializer

        async Task IConnectionInitializer.InitializeConnectionAsync(IConnection connection, CancellationToken cancellationToken)
        {
            if (!_disposed)
            {
                _logger.LogDebug("Starting connection initialization on server {endpoint}.", EndPoint);
                using var rootSpan = RootSpan("initialize_connection");
                ServerFeatures = await Hello(connection, rootSpan, cancellationToken).ConfigureAwait(false);
                ErrorMap = await GetErrorMap(connection, rootSpan, cancellationToken).ConfigureAwait(false);

                var mechanismType = _context.ClusterOptions.EffectiveEnableTls
                    ? MechanismType.Plain
                    : MechanismType.ScramSha1;
                var saslMechanism = _saslMechanismFactory.Create(mechanismType, _context.ClusterOptions.UserName,
                    _context.ClusterOptions.Password);

                await saslMechanism.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task IConnectionInitializer.SelectBucketAsync(IConnection connection, string bucketName, CancellationToken cancellationToken)
        {
            try
            {
                using var rootSpan = RootSpan(OperationNames.SelectBucket);
                using var selectBucketOp = new SelectBucket
                {
                    Transcoder = _transcoder,
                    Key = bucketName,
                    Span = rootSpan,
                };
                await ExecuteOp(connection, selectBucketOp, cancellationToken).ConfigureAwait(false);
            }
            catch (DocumentNotFoundException)
            {
                //If DNF exception then BucketNotConnected was returned so close the connection and let it get cleaned up later
                await connection.CloseAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                var message = "The Bucket [" + _redactor.MetaData(bucketName) + "] could not be selected. Either it does not exist, " +
                              "is unavailable or the node itself does not have the Data service enabled.";

                _logger.LogError(LoggingEvents.BootstrapEvent, message);
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _logger.LogDebug("Disposing cluster node for {endpoint}", EndPoint);
            ConnectionPool?.Dispose();
        }

        #region Equality

        public bool Equals(ClusterNode other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Owner, other.Owner) && BootstrapEndpoint.Equals(other.BootstrapEndpoint);
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
            return new { EndPoint, _id }.GetHashCode();
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

        private IInternalSpan RootSpan(string operation) =>
            _tracer.RootSpan(CouchbaseTags.Service, operation);

        private IInternalSpan RootSpan(string operation, OperationBase op) => RootSpan(operation).OperationId(op);
        #endregion
    }
}
