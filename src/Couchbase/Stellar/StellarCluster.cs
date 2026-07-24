#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Diagnostics;
using Couchbase.Client.Transactions;
using Couchbase.Core;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Stellar.Analytics;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Management.Buckets;
using Couchbase.Stellar.Management.Query;
using Couchbase.Stellar.Management.Search;
using Couchbase.Stellar.Query;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Couchbase.Utils;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Stellar;

#nullable enable

internal class StellarCluster : ICluster, IBootstrappable, IClusterExtended
{
    private readonly ClusterOptions _clusterOptions;
    private readonly List<Exception> _deferredExceptions = new();
    private readonly IBucketManager _bucketManager;
    private readonly ISearchIndexManager _searchIndexManager;
    private readonly IQueryIndexManager _queryIndexManager;
    private readonly IAnalyticsClient _analyticsClient;
    private readonly IStellarSearchClient _searchClient;
    private readonly IQueryClient _queryClient;
    private Metadata _metaData;
    private readonly ConcurrentDictionary<string, IBucket> _buckets = new();
    private readonly ILogger<StellarCluster> _logger;
    private readonly IRedactor _redactor;
    private volatile bool _disposed;
    private readonly IServiceProvider _clusterServices;
    private readonly bool _isCompressionEnabled;

    // The SocketsHttpHandler backing the gRPC channel when this cluster created the channel
    // itself (the real-connect path). gRPC does not take ownership of a caller-supplied handler
    // unless GrpcChannelOptions.DisposeHttpClient is set, so we keep a reference and dispose it
    // ourselves. Null when the channel was injected (e.g. unit tests), in which case the channel
    // owns its transport.
    private readonly SocketsHttpHandler? _socketsHandler;

    // Standard gRPC health-check client used by WaitUntilReady (NCBC-4269 / RFC 77 CNG-1).
    // Settable so unit tests can inject a mock.
    internal Health.HealthClient HealthClient { get; set; }


    internal StellarCluster(IBucketManager bucketManager, ISearchIndexManager searchIndexManager,
        IQueryIndexManager queryIndexManager, IQueryClient queryClient,
        IAnalyticsClient analyticsClient, IStellarSearchClient searchClient,
        Metadata metaData, IRequestTracer requestTracer, GrpcChannel grpcChannel,
        ITypeSerializer typeSerializer, IRetryOrchestrator retryHandler, ClusterOptions clusterOptions,
        IOperationCompressor operationCompressor,
        CompressionAlgorithm compressionAlgorithm = CompressionAlgorithm.None)
    {
        _bucketManager = bucketManager;
        _searchIndexManager = searchIndexManager;
        _queryClient = queryClient;
        _analyticsClient = analyticsClient;
        _searchClient = searchClient;
        _clusterOptions = clusterOptions;
        _queryIndexManager = queryIndexManager;
        _metaData = metaData;
        RequestTracer = requestTracer;
        GrpcChannel = grpcChannel;
        HealthClient = new Health.HealthClient(grpcChannel);
        TypeSerializer = typeSerializer;
        TypeTranscoder = _clusterOptions.Transcoder ?? new JsonTranscoder(TypeSerializer);
        OperationCompressor = operationCompressor;
        _isCompressionEnabled = _clusterOptions.Compression && compressionAlgorithm != CompressionAlgorithm.None;
        RetryHandler = retryHandler;
        _redactor = new Redactor(new TypedRedactor(_clusterOptions));
        _logger = new Logger<StellarCluster>(_clusterOptions.Logging ?? new NullLoggerFactory());
        _clusterServices = new StellarServiceProvider(this);
    }

    private StellarCluster(ClusterOptions clusterOptions)
    {
        _clusterOptions = clusterOptions;
        _redactor = new Redactor(new TypedRedactor(_clusterOptions));
        _logger = new Logger<StellarCluster>(_clusterOptions.Logging ?? new NullLoggerFactory());
        RequestTracer = clusterOptions.TracingOptions.RequestTracer;
        TypeSerializer = clusterOptions.Serializer ?? DefaultSerializer.Instance;
        TypeTranscoder = clusterOptions.Transcoder ?? new JsonTranscoder(TypeSerializer);

        var serviceProvider = clusterOptions.BuildServiceProvider();
        OperationCompressor = serviceProvider.GetRequiredService<IOperationCompressor>();
        var compressionAlgorithm = serviceProvider.GetRequiredService<ICompressionAlgorithm>();
        _isCompressionEnabled = _clusterOptions.Compression && compressionAlgorithm.Algorithm != CompressionAlgorithm.None;

        var socketsHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(20),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
        };
        _socketsHandler = socketsHandler;
        var authenticator = _clusterOptions.GetEffectiveAuthenticator() ?? throw new NullReferenceException($"{nameof(_clusterOptions.Authenticator)} should not be null");
        var certificateCallbackFactory = new CertificateValidationCallbackFactory(_clusterOptions, new Logger<CertificateValidationCallbackFactory>(_clusterOptions.Logging ?? new NullLoggerFactory()), _redactor);

        authenticator.AuthenticateHttpHandler(socketsHandler, clusterOptions, certificateCallbackFactory, _logger);

        var grpcChannelOptions = new GrpcChannelOptions()
        {
            LoggerFactory = clusterOptions.Logging,
            HttpHandler = socketsHandler,
            MaxReceiveMessageSize = 25 * 1024 * 1024, // 25 MiB (26214400 bytes) per RFC 77; accommodates the 20MB max doc plus overhead (gRPC default of 4MB is too small)
        };

        GrpcChannel = GrpcChannel.ForAddress(_clusterOptions.ConnectionStringValue!.GetStellarBootstrapUri(), grpcChannelOptions);
        HealthClient = new Health.HealthClient(GrpcChannel);
        RetryHandler = new StellarRetryHandler();

        _bucketManager = new StellarBucketManager(this);
        _searchIndexManager = new StellarSearchIndexManager(this);
        _queryIndexManager = new StellarQueryIndexManager(this);
        _queryClient = new StellarQueryClient(this,
            new QueryService.QueryServiceClient(GrpcChannel),
            TypeSerializer,
            RetryHandler);
        _metaData = new Metadata();
        _analyticsClient = new StellarAnalyticsClient(this,
            new AnalyticsService.AnalyticsServiceClient(GrpcChannel),
            RetryHandler);
        _searchClient = new StellarSearchClient(this);

        authenticator.AuthenticateGrpcMetadata(_metaData);
        _clusterServices = new StellarServiceProvider(this);
    }

    public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        clusterOptions ??= new ClusterOptions();
        var opts = clusterOptions.WithConnectionString(connectionString);
        return await ConnectAsync(opts).ConfigureAwait(false);
    }

    public static Task<ICluster> ConnectAsync(ClusterOptions clusterOptions)
    {
        // Note: We intentionally do NOT call GrpcChannel.ConnectAsync() here.
        // Pre-connecting creates a raw TCP socket via the subchannel transport
        // that sits idle until the first RPC. Proxies (HAProxy/nginx on OpenShift)
        // kill idle sockets before HTTP/2 can be negotiated, causing permanent
        // "unable to establish HTTP/2 connection" failures (grpc-dotnet#2343).
        // Instead, the channel connects lazily on the first RPC, ensuring the
        // socket connect and HTTP/2 negotiation happen in one shot.
        var cluster = new StellarCluster(clusterOptions);
        return Task.FromResult<ICluster>(cluster);
    }

    internal IRequestTracer RequestTracer { get; }
    internal ITypeTranscoder TypeTranscoder { get; }
    internal IOperationCompressor OperationCompressor { get; }
    internal bool IsCompressionEnabled => _isCompressionEnabled;

    private void CheckIfDisposed()
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(StellarCluster));
        }
    }

    private async Task ConnectGrpcAsync(TimeSpan kvConnectTimeout)
    {
        CheckIfDisposed();

        var stopwatch = new Stopwatch();
        using var cts = new CancellationTokenSource(kvConnectTimeout);
        try
        {
            _deferredExceptions.Clear();
            stopwatch.Start();
            await GrpcChannel.ConnectAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e)
        {
            stopwatch.Stop();
            if (cts.IsCancellationRequested)
            {
                var ex = new Couchbase.Core.Exceptions.TimeoutException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ConnectTimeoutExceptionMsg,
                        _clusterOptions.ConnectionString, stopwatch.Elapsed.TotalSeconds, kvConnectTimeout),
                    e);
                _deferredExceptions.Add(ex);
            }
            else
            {
                _deferredExceptions.Add(new ConnectException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ConnectException, _clusterOptions.ConnectionString), e));
            }
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            _deferredExceptions.Add(new ConnectException(
                ExceptionUtil.GetMessage(ExceptionUtil.ConnectException, _clusterOptions.ConnectionString), e));
        }
        finally
        {
            stopwatch.Stop();
        }
    }
    internal IRetryOrchestrator RetryHandler { get; }

    internal GrpcChannel GrpcChannel { get; }

    /// <summary>
    /// The <see cref="SocketsHttpHandler"/> this cluster created and owns, or <c>null</c> when the
    /// gRPC channel was injected. Exposed for tests to verify it is disposed with the cluster.
    /// </summary>
    internal SocketsHttpHandler? OwnedHttpHandler => _socketsHandler;

    internal ClusterOptions ClusterOptions => _clusterOptions;

    internal ITypeSerializer TypeSerializer { get; }

    public IServiceProvider ClusterServices => _clusterServices;

    public IQueryIndexManager QueryIndexes
    {
        get
        {
            CheckIfDisposed();
            return _queryIndexManager;
        }
    }

    public IAnalyticsIndexManager AnalyticsIndexes =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Analytics Index Management", "Protostellar");

    public ISearchIndexManager SearchIndexes
    {
        get
        {
            CheckIfDisposed();
            return _searchIndexManager;
        }
    }

    public IBucketManager Buckets
    {
        get
        {
            CheckIfDisposed();
            return _bucketManager;
        }
    }

    public IUserManager Users =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("User Management", "Protostellar");

    public IEventingFunctionManager EventingFunctions =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Eventing Functions", "Protostellar");

    public Transactions Transactions =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Transactions", "Protostellar");

    public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = null)
    {
        CheckIfDisposed();
        ThrowIfBootStrapFailed();

        options ??= new AnalyticsOptions();
        return await _analyticsClient.QueryAsync<T>(statement, options).ConfigureAwait(false);
    }

    public ValueTask<IBucket> BucketAsync(string name)
    {
        CheckIfDisposed();
        return new ValueTask<IBucket>(_buckets.GetOrAdd(name, new StellarBucket(name, this)));
    }

    #region Diagnostics - Not Supported

    public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Diagnostics", "Protostellar");

    public Task<IPingReport> PingAsync(PingOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Ping", "Protostellar");

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var bucket in _buckets.Values)
        {
            bucket.Dispose();
        }
        GrpcChannel.Dispose();

        // Dispose the handler we own so its connection pool and keep-alive ping timers are torn
        // down promptly. GrpcChannel.Dispose() does not do this for a caller-supplied handler.
        _socketsHandler?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
    {
        CheckIfDisposed();
        ThrowIfBootStrapFailed();

        return _queryClient.QueryAsync<T>(statement, options!);
    }

    public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = null)
    {
        CheckIfDisposed();
        ThrowIfBootStrapFailed();

        return await _searchClient.QueryAsync(indexName, query, options).ConfigureAwait(false);
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
    {
        CheckIfDisposed();
        var opts = options ?? new WaitUntilReadyOptions();

        // RFC 77 (couchbase2 WaitUntilReady): ping the standard gRPC health-check RPC until the
        // server reports SERVING or the timeout is exceeded. The ServiceTypes and DesiredState
        // options are silently ignored (the health check exposes no per-service or cluster-state
        // granularity), no Authorization header is sent, and no metrics are emitted.
        using var traceSpan = RequestTracer.RequestSpan("wait_until_ready", null);

        var request = new StellarRequest
        {
            // A health check is read-only: safe to retry, and unambiguous on timeout.
            Idempotent = true,
            Timeout = timeout,
            Token = opts.CancellationTokenValue,
            Span = traceSpan,
        };

        await RetryHandler.RetryAsync(async () =>
        {
            // No Authorization header per RFC (the server does not inspect it). The per-attempt
            // deadline is the remaining WaitUntilReady budget, so once it is exhausted the call
            // fails DEADLINE_EXCEEDED and is mapped to an UnambiguousTimeoutException.
            var callOptions = new CallOptions(
                headers: new Metadata(),
                deadline: request.RemainingTimeout.FromNow(),
                cancellationToken: request.Token);

            var response = await HealthClient.CheckAsync(new HealthCheckRequest(), callOptions);

            if (response.Status != HealthCheckResponse.Types.ServingStatus.Serving)
            {
                // Not SERVING: retry per RFC (SERVICE_NOT_AVAILABLE). Signalled as UNAVAILABLE so the
                // standard retry path handles it and records the serving status as the last error.
                throw new RpcException(new Status(StatusCode.Unavailable,
                    $"couchbase2 server reported serving status {response.Status}"));
            }

            return WaitUntilReadyResult.Instance;
        }, request).ConfigureAwait(false);
    }

    // WaitUntilReady has no service payload; this satisfies the IServiceResult constraint on
    // StellarRetryHandler.RetryAsync so the health check reuses the standard retry rules.
    private sealed class WaitUntilReadyResult : IServiceResult
    {
        public static readonly WaitUntilReadyResult Instance = new();
        public RetryReason RetryReason => RetryReason.NoRetry;
    }

    public CallOptions GrpcCallOptions() => new (headers: _metaData);

    public CallOptions GrpcCallOptions(CancellationToken cancellationToken) => new (headers: _metaData, cancellationToken: cancellationToken);

    public CallOptions GrpcCallOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
        new (headers: _metaData, deadline: timeout.FromNow(), cancellationToken: cancellationToken);

    private IRequestSpan TraceSpan(string attr, IRequestSpan? parentSpan) =>
        this.RequestTracer.RequestSpan(attr, parentSpan);

    #region Bootstrapping/start up error propagation

    public Task BootStrapAsync(CancellationToken cancellationToken = default) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("BootStrap", "Protostellar");

    public bool IsBootstrapped => _deferredExceptions.Count == 0;

    public List<Exception> DeferredExceptions => _deferredExceptions;


    /// <summary>
    /// Throw an exception if the bucket is not bootstrapped successfully.
    /// </summary>
    internal void ThrowIfBootStrapFailed()
    {
        if (!IsBootstrapped)
        {
            ThrowBootStrapFailed();
        }
    }

    /// <summary>
    /// Throw am AggregateException with deferred bootstrap exceptions.
    /// </summary>
    /// <remarks>
    /// This is a separate method from <see cref="ThrowIfBootStrapFailed"/> to allow that method to
    /// be inlined for the fast, common path where there the bucket is bootstrapped.
    /// </remarks>
    private void ThrowBootStrapFailed()
    {
        throw new AggregateException($"Bootstrapping for the cluster as failed.", DeferredExceptions);
    }

    #endregion

    /// <summary>
    /// Updates the authenticator used by this cluster.
    /// </summary>
    /// <param name="authenticator">The new authenticator to use.</param>
    public void Authenticator(IAuthenticator authenticator)
    {
        if (authenticator == null) throw new ArgumentNullException(nameof(authenticator));

        _clusterOptions.Authenticator = authenticator;

        var newMetaData = new Metadata();
        authenticator.AuthenticateGrpcMetadata(newMetaData);

        // Update the metadata instance used for all subsequent gRPC calls
        _metaData = newMetaData;
    }

    /// <inheritdoc />
    void IClusterExtended.RemoveBucket(string bucketName)
    {
        _buckets.TryRemove(bucketName, out _);
    }

    /// <inheritdoc />
    bool IClusterExtended.BucketExists(string bucketName)
    {
        return _buckets.TryGetValue(bucketName, out _);
    }

    /// <summary>
    /// Minimal <see cref="IServiceProvider"/> that exposes the services already
    /// held by <see cref="StellarCluster"/> through the standard
    /// <see cref="ICluster.ClusterServices"/> contract.
    /// </summary>
    private sealed class StellarServiceProvider : IServiceProvider
    {
        private readonly StellarCluster _cluster;

        public StellarServiceProvider(StellarCluster cluster) => _cluster = cluster;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRequestTracer))
                return _cluster.RequestTracer;
            if (serviceType == typeof(ITypeTranscoder))
                return _cluster.TypeTranscoder;
            if (serviceType == typeof(ITypeSerializer))
                return _cluster.TypeSerializer;
            if (serviceType == typeof(IRedactor))
                return _cluster._redactor;
            if (serviceType == typeof(ILoggerFactory))
                return _cluster._clusterOptions.Logging;

            return null;
        }
    }
}
#endif
