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
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterNode : IClusterNode
    {
        private static readonly ILogger Log = LogManager.CreateLogger<ClusterNode>();
        private readonly ClusterContext _context;
        private static readonly TimeSpan DefaultTimeout = new TimeSpan(0, 0, 0, 0, 2500);//temp
        private readonly ITypeTranscoder _transcoder = new DefaultTranscoder();
        private Uri _queryUri;
        private Uri _analyticsUri;
        private Uri _searchUri;
        private Uri _viewsUri;
        private CircuitBreaker _circuitBreaker = new CircuitBreaker();//TODO integrate with configuration

        public ClusterNode(ClusterContext context)
        {
            _context = context;
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

        //TODO these methods will be more complex once we have a cpool
        public Task<Manifest> GetManifest()
        {
           return Connection.GetManifest();
        }

        public Task SelectBucket(string name)
        {
            return Connection.SelectBucket(name);
        }

        public async Task<BucketConfig> GetClusterMap()
        {
            using var configOp = new Config
            {
                CurrentHost = EndPoint,
                Transcoder = new DefaultTranscoder(),
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = EndPoint
            };
            await ExecuteOp(configOp);

            var configResult = configOp.GetResultWithValue();
            var config = configResult.Content;

            if (config != null && BootstrapUri != null)
            {
                config.ReplacePlaceholderWithBootstrapHost(BootstrapUri);
            }

            return config;
        }

        public void BuildServiceUris()
        {
            if (NodesAdapter != null)
            {
                QueryUri = EndPoint.GetQueryUri(_context.ClusterOptions, NodesAdapter);
                SearchUri = EndPoint.GetSearchUri(_context.ClusterOptions, NodesAdapter);
                AnalyticsUri = EndPoint.GetAnalyticsUri(_context.ClusterOptions, NodesAdapter);
                ViewsUri = EndPoint.GetViewsUri(_context.ClusterOptions, NodesAdapter); //TODO move to IBucket level?
                ManagementUri = EndPoint.GetManagementUri(_context.ClusterOptions, NodesAdapter);
            }
        }

        public async Task SendAsync(IOperation op,
            CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            Log.LogDebug($"CB: Current state is {_circuitBreaker.State}.");

            if (_circuitBreaker.Enabled)
            {
                if (_circuitBreaker.AllowsRequest())
                {
                    _circuitBreaker.Track();
                    try
                    {
                        Log.LogDebug($"CB: Sending {op.Opaque} to {Connection.EndPoint}.");
                        await ExecuteOp(Connection, op, token);
                        _circuitBreaker.MarkSuccess();
                    }
                    catch (Exception e)
                    {
                        if (_circuitBreaker.CompletionCallback(e))
                        {
                            Log.LogDebug($"CB: Marking a failure for {op.Opaque} to {Connection.EndPoint}.");
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
                            Log.LogDebug($"CB: Sending a canary to {Connection.EndPoint}.");
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
                                Log.LogDebug($"CB: Marking a failure for canary sent to {Connection.EndPoint}.");
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
            Log.LogDebug("Executing op {0} with key {1} and opaque {2}", op.OpCode, op.Key, op.Opaque);

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
                    ErrorMap.TryGetGetErrorCode((short) state.Status, out ErrorCode errorCode);
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
                    cts.CancelAfter(timeout.HasValue && timeout != TimeSpan.Zero ? timeout.Value : DefaultTimeout);
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

                    Log.LogDebug("Completed executing op {0} with key {1} and opaque {2}", op.OpCode, op.Key,
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

        protected async Task CheckConnectionAsync(IConnection connection)
        {
            if (connection.IsDead)
            {
                //recreate the connection its been closed and disposed
                connection = EndPoint.GetConnection(_context.ClusterOptions);
                ServerFeatures = await connection.Hello().ConfigureAwait(false);
                ErrorMap = await connection.GetErrorMap().ConfigureAwait(false);
                await connection.Authenticate(_context.ClusterOptions, Owner.Name).ConfigureAwait(false);
                await connection.SelectBucket(Owner.Name).ConfigureAwait(false);
                Connection = connection;
            }
        }

        public static async Task<IClusterNode> CreateAsync(ClusterContext context, IPEndPoint endPoint)
        {
            var connection = endPoint.GetConnection(context.ClusterOptions);
            var serverFeatures = await connection.Hello().ConfigureAwait(false);
            var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
            await connection.Authenticate(context.ClusterOptions, null, context.CancellationToken).ConfigureAwait(false);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = endPoint,
                Connection = connection,
                ServerFeatures = serverFeatures,
                ErrorMap = errorMap
            };
            clusterNode.BuildServiceUris();
            return clusterNode;
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }
    }
}
