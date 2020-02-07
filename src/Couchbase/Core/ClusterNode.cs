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
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterNode : IClusterNode
    {
        private readonly ClusterContext _context;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<ClusterNode> _logger;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ITypeTranscoder _transcoder;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private Uri _queryUri;
        private Uri _analyticsUri;
        private Uri _searchUri;
        private Uri _viewsUri;

        public ClusterNode(ClusterContext context, IConnectionFactory connectionFactory, ILogger<ClusterNode> logger, ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentException(nameof(saslMechanismFactory));
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
        public NodeAdapter NodesAdapter { get; set; }
        public Uri BootstrapUri { get; set; }
        public IPEndPoint EndPoint { get; set; }

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
        public IConnection Connection { get; set; }//TODO this will be a connection pool later NOTE: group these by IBucket!
        public List<Exception> Exceptions { get; set; }//TODO catch and hold until first operation per RFC
        public bool HasViews => NodesAdapter.IsViewNode;
        public bool HasAnalytics => NodesAdapter.IsAnalyticsNode;
        public bool HasQuery => NodesAdapter.IsQueryNode;
        public bool HasSearch => NodesAdapter.IsSearchNode;
        public bool HasKv => NodesAdapter.IsKvNode;

        public ConcurrentDictionary<IBucket, IConnection> Connections = new ConcurrentDictionary<IBucket, IConnection>();

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
            await InitializeAsync(Connection);
        }

        private async Task InitializeAsync(IConnection connection)
        {
            ServerFeatures = await Hello(connection).ConfigureAwait(false);
            ErrorMap = await GetErrorMap(connection).ConfigureAwait(false);

            var mechanismType = _context.ClusterOptions.EnableTls ? MechanismType.Plain : MechanismType.ScramSha1;
            var saslMechanism = _saslMechanismFactory.Create(mechanismType, _context.ClusterOptions.UserName,
                _context.ClusterOptions.Password);

            await saslMechanism.AuthenticateAsync(connection, _context.CancellationToken).ConfigureAwait(false);
            await SelectBucket(connection, Owner.Name).ConfigureAwait(false);
        }

        private async Task SelectBucket(IConnection connection, string bucketName)
        {
            using var selectBucketOp = new SelectBucket
            {
                Transcoder = _transcoder,
                Key = bucketName
            };
            await ExecuteOp(selectBucketOp).ConfigureAwait(false);
        }

        private async Task<ErrorMap> GetErrorMap(IConnection connection)
        {
            using var errorMapOp = new GetErrorMap
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext()
            };

            await ExecuteOp(errorMapOp).ConfigureAwait(false);
            return errorMapOp.GetResultWithValue().Content;
        }

        private async Task<short[]> Hello(IConnection connection)
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
                Key = Core.IO.Operations.Hello.BuildHelloKey(Connection.ConnectionId),
                Content = features.ToArray(),
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext(),
            };

            await ExecuteOp(heloOp).ConfigureAwait(false);
            return heloOp.GetResultWithValue().Content;
        }

        public async Task<Manifest> GetManifest()
        {
            using var manifestOp = new GetManifest
            {
                Transcoder = _transcoder,
                Opaque = SequenceGenerator.GetNext()
            };
            await ExecuteOp(manifestOp);
            var manifestResult = manifestOp.GetResultWithValue();
            return manifestResult.Content;
        }

        public async Task SelectBucket(string name)
        {
            using var selectBucketOp = new SelectBucket
            {
                Transcoder = _transcoder,
                Key = name
            };

            await ExecuteOp(selectBucketOp);
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
            await ExecuteOp(configOp);

            var configResult = configOp.GetResultWithValue();
            var config = configResult.Content;

            if (config != null && EndPoint!= null)
            {
                config.ReplacePlaceholderWithBootstrapHost(BootstrapUri);
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

            await ExecuteOp(getCid).ConfigureAwait(false);
            var resultWithValue = getCid.GetResultWithValue();
            return resultWithValue.Content;
        }

        public void BuildServiceUris()
        {
            if (NodesAdapter != null)
            {
                QueryUri = NodesAdapter.GetQueryUri(_context.ClusterOptions);
                SearchUri = NodesAdapter.GetSearchUri(_context.ClusterOptions);
                AnalyticsUri = NodesAdapter.GetAnalyticsUri(_context.ClusterOptions);
                ViewsUri = NodesAdapter.GetViewsUri(_context.ClusterOptions); //TODO move to IBucket level?
                ManagementUri = NodesAdapter.GetManagementUri(_context.ClusterOptions);
            }
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
                        _logger.LogDebug("CB: Sending {opaque} to {endPoint}.", op.Opaque, Connection.EndPoint);
                        await ExecuteOp(Connection, op, token);
                        _circuitBreaker.MarkSuccess();
                    }
                    catch (Exception e)
                    {
                        if (_circuitBreaker.CompletionCallback(e))
                        {
                            _logger.LogDebug("CB: Marking a failure for {opaque} to {endPoint}.", op.Opaque, Connection.EndPoint);
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
                            _logger.LogDebug("CB: Sending a canary to {endPoint}.", Connection.EndPoint);
                            using (var cts = new CancellationTokenSource(_circuitBreaker.CanaryTimeout))
                            {
                                await ExecuteOp(Connection, new Noop(), cts.Token);
                            }

                            _circuitBreaker.MarkSuccess();
                        }
                        catch (Exception e)
                        {
                            if (_circuitBreaker.CompletionCallback(e))
                            {
                                _logger.LogDebug("CB: Marking a failure for canary sent to {endPoint}.", Connection.EndPoint);
                                _circuitBreaker.MarkFailure();
                            }
                        }
                    }

                    throw new CircuitBreakerException();
                }
            }
            else
            {
                await ExecuteOp(Connection, op, token);
            }
        }

        public async Task ExecuteOp(IConnection connection, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            _logger.LogDebug("Executing op {0} with key {1} and opaque {2}", op.OpCode, op.Key, op.Opaque);

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
                    await CheckConnectionAsync(connection);
                    await op.SendAsync(connection).ConfigureAwait(false);
                    var bytes = await tcs.Task.ConfigureAwait(false);
                    await op.ReadAsync(bytes).ConfigureAwait(false);

                    var status = op.Header.Status;
                    if (status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        var config = op.GetConfig(_transcoder);
                        _context.PublishConfig(config);
                    }

                    _logger.LogDebug("Completed executing op {opCode} with key {key} and opaque {opaque}", op.OpCode,
                        op.Key,
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
            return ExecuteOp(Connection, op, token);
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

        protected async Task CheckConnectionAsync(IConnection connection)
        {
            if (connection.IsDead)
            {
                //recreate the connection its been closed and disposed
                connection = await _connectionFactory.CreateAndConnectAsync(EndPoint).ConfigureAwait(false);
                await InitializeAsync(connection).ConfigureAwait(false);
                Connection = connection;
            }
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }
    }
}
