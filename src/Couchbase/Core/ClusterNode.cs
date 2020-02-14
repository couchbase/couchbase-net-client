using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterNode : IClusterNode, IConnectionInitializer
    {
        private readonly ClusterContext _context;
        private readonly ILogger<ClusterNode> _logger;
        private readonly IRedactor _redactor;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ITypeTranscoder _transcoder;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private Uri _queryUri;
        private Uri _analyticsUri;
        private Uri _searchUri;
        private Uri _viewsUri;
        private NodeAdapter _nodesAdapter;

        public ClusterNode(ClusterContext context, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger, ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory, IRedactor redactor, IPEndPoint endPoint)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentException(nameof(saslMechanismFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));

            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));

            if (connectionPoolFactory == null)
            {
                throw new ArgumentNullException(nameof(connectionPoolFactory));
            }

            ConnectionPool = connectionPoolFactory.Create(this);
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
        public IBucket Owner { get; private set; }

        public NodeAdapter NodesAdapter
        {
            get => _nodesAdapter;
            set
            {
                _nodesAdapter = value;
                BuildServiceUris();
            }
        }

        public HostEndpoint BootstrapEndpoint { get; set; }
        public IPEndPoint EndPoint { get; }

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
        public bool HasViews => NodesAdapter.IsViewNode;
        public bool HasAnalytics => NodesAdapter.IsAnalyticsNode;
        public bool HasQuery => NodesAdapter.IsQueryNode;
        public bool HasSearch => NodesAdapter.IsSearchNode;
        public bool HasKv => NodesAdapter.IsKvNode;

        public bool Supports(ServerFeatures feature)
        {
            return ServerFeatures.Contains((short) feature);
        }

        public DateTime? LastViewActivity { get; private set; }
        public DateTime? LastQueryActivity { get; private set; }
        public DateTime? LastSearchActivity { get; private set; }
        public DateTime? LastAnalyticsActivity { get; private set; }
        public DateTime? LastKvActivity { get; private set; }

        public async Task InitializeAsync()
        {
            await ConnectionPool.InitializeAsync(_context.CancellationToken);
        }

        private async Task<ErrorMap> GetErrorMap(IConnection connection, CancellationToken cancellationToken = default)
        {
            using var errorMapOp = new GetErrorMap
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext()
            };

            await ExecuteOp(connection, errorMapOp, cancellationToken).ConfigureAwait(false);
            return errorMapOp.GetResultWithValue().Content;
        }

        private async Task<short[]> Hello(IConnection connection, CancellationToken cancellationToken = default)
        {
            var features = new List<short>
            {
                (short) IO.Operations.ServerFeatures.SelectBucket,
                (short) IO.Operations.ServerFeatures.Collections,
                (short) IO.Operations.ServerFeatures.AlternateRequestSupport,
                (short) IO.Operations.ServerFeatures.SynchronousReplication,
                (short) IO.Operations.ServerFeatures.SubdocXAttributes,
                (short) IO.Operations. ServerFeatures.XError
            };

            if (_context.ClusterOptions.EnableMutationTokens)
            {
                features.Add((short) IO.Operations.ServerFeatures.MutationSeqno);
            }

            if (_context.ClusterOptions.EnableOperationDurationTracing)
            {
                features.Add((short) IO.Operations.ServerFeatures.ServerDuration);
            }

            using var heloOp = new Hello
            {
                Key = Core.IO.Operations.Hello.BuildHelloKey(connection.ConnectionId),
                Content = features.ToArray(),
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
            };

            await ExecuteOp(connection, heloOp, cancellationToken).ConfigureAwait(false);
            return heloOp.GetResultWithValue().Content;
        }

        public async Task<Manifest> GetManifest()
        {
            using var manifestOp = new GetManifest
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext()
            };
            await ExecuteOp(ConnectionPool, manifestOp);
            var manifestResult = manifestOp.GetResultWithValue();
            return manifestResult.Content;
        }

        public async Task SelectBucketAsync(IBucket bucket, CancellationToken cancellationToken = default)
        {
            await ConnectionPool.SelectBucketAsync(bucket.Name, cancellationToken);

            Owner = bucket;
        }

        public async Task<BucketConfig> GetClusterMap()
        {
            using var configOp = new Config
            {
                CurrentHost = EndPoint,
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = EndPoint,
            };
            await ExecuteOp(ConnectionPool, configOp);

            var configResult = configOp.GetResultWithValue();
            var config = configResult.Content;

            if (config != null && EndPoint!= null)
            {
                config.ReplacePlaceholderWithBootstrapHost(BootstrapEndpoint.Host);
            }

            return config;
        }

        public async Task<uint?> GetCid(string fullyQualifiedName)
        {
            using var getCid = new GetCid
            {
                Key = fullyQualifiedName,
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
                Content = null
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

        public async Task SendAsync(IOperation op,
            CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
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
                                await ExecuteOp(ConnectionPool, new Noop(), cts.Token).ConfigureAwait(false);
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

        private async Task ExecuteOp(Func<Task> sender, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            _logger.LogDebug("Executing op {opcode} with key {key} and opaque {opaque}", op.OpCode, _redactor.UserData(op.Key), op.Opaque);

            // wire up op's completed function
            var tcs = new TaskCompletionSource<IMemoryOwner<byte>>();
            op.Completed = state =>
            {
                if (state.Status == ResponseStatus.Success)
                {
                    tcs.TrySetResult(state.ExtractData());
                }
                else if
                    (state.Status == ResponseStatus.VBucketBelongsToAnotherServer)
                {
                    tcs.TrySetResult(state.ExtractData());
                }
                else
                {
                    var code = (short) state.Status;
                    if (!ErrorMap.TryGetGetErrorCode(code, out var errorCode))
                    {
                        _logger.LogWarning("Unexpected Status for KeyValue operation not found in Error Map: 0x{code}", code.ToString("X4"));
                    }

                    tcs.TrySetException(state.ThrowException(errorCode));
                }

                return tcs.Task;
            };

            CancellationTokenSource cts = null;
            try
            {
                if (token == CancellationToken.None)
                {
                    cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(GetTimeout(timeout, op));
                    token = cts.Token;
                }

                using (token.Register(() =>
                {
                    if (tcs.Task.Status != TaskStatus.RanToCompletion)
                    {
                        tcs.TrySetCanceled();
                    }
                }, useSynchronizationContext: false))
                {
                    await sender().ConfigureAwait(false);
                    var bytes = await tcs.Task.ConfigureAwait(false);
                    await op.ReadAsync(bytes).ConfigureAwait(false);

                    var status = op.Header.Status;
                    if (status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        var config = op.GetConfig(_transcoder);
                        _context.PublishConfig(config);
                    }

                    _logger.LogDebug("Completed executing op {opCode} with key {key} and opaque {opaque}", op.OpCode,
                       _redactor.UserData(op.Key),
                        op.Opaque);
                }
            }
            catch (OperationCanceledException e)
            {
                if (!e.CancellationToken.IsCancellationRequested)
                {
                    //oddly IsCancellationRequested is false when timed out
                    throw new TimeoutException();
                }
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
            return ExecuteOp(() => connectionPool.SendAsync(op, token), op, token);
        }

        public Task ExecuteOp(IConnection connection, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            return ExecuteOp(() => op.SendAsync(connection), op, token);
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
            ServerFeatures = await Hello(connection, cancellationToken).ConfigureAwait(false);
            ErrorMap = await GetErrorMap(connection, cancellationToken).ConfigureAwait(false);

            var mechanismType = _context.ClusterOptions.EffectiveEnableTls ? MechanismType.Plain : MechanismType.ScramSha1;
            var saslMechanism = _saslMechanismFactory.Create(mechanismType, _context.ClusterOptions.UserName,
                _context.ClusterOptions.Password);

            await saslMechanism.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        async Task IConnectionInitializer.SelectBucketAsync(IConnection connection, string bucketName, CancellationToken cancellationToken)
        {
            using var selectBucketOp = new SelectBucket
            {
                Transcoder = _transcoder,
                Key = bucketName
            };
            await ExecuteOp(connection, selectBucketOp, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        public void Dispose()
        {
            ConnectionPool?.Dispose();
        }
    }
}
