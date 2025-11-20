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
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Diagnostics;
using Couchbase.Client.Transactions;
using Couchbase.Core;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
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
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly Metadata _metaData;
    private readonly ConcurrentDictionary<string, IBucket> _buckets = new();
    private readonly ILogger<StellarCluster> _logger;
    private readonly IRedactor _redactor;
    private volatile bool _disposed;


    internal StellarCluster(IBucketManager bucketManager, ISearchIndexManager searchIndexManager,
        IQueryIndexManager queryIndexManager, IQueryClient queryClient,
        IAnalyticsClient analyticsClient, IStellarSearchClient searchClient,
        Metadata metaData, IRequestTracer requestTracer, GrpcChannel grpcChannel,
        ITypeSerializer typeSerializer, IRetryOrchestrator retryHandler, ClusterOptions clusterOptions)
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
        TypeSerializer = typeSerializer;
        TypeTranscoder = _clusterOptions.Transcoder ?? new JsonTranscoder(TypeSerializer);
        RetryHandler = retryHandler;
        _redactor = new Redactor(new TypedRedactor(_clusterOptions));
        _logger = new Logger<StellarCluster>(_clusterOptions.Logging ?? new NullLoggerFactory());
    }

    private StellarCluster(ClusterOptions clusterOptions)
    {
        _clusterOptions = clusterOptions;
        _redactor = new Redactor(new TypedRedactor(_clusterOptions));
        _logger = new Logger<StellarCluster>(_clusterOptions.Logging ?? new NullLoggerFactory());
        RequestTracer = clusterOptions.TracingOptions.RequestTracer;
        TypeSerializer = clusterOptions.Serializer ?? DefaultSerializer.Instance;
        TypeTranscoder = clusterOptions.Transcoder ?? new JsonTranscoder(TypeSerializer);

        var socketsHandler = new SocketsHttpHandler();
        var authenticator = _clusterOptions.GetEffectiveAuthenticator() ?? throw new NullReferenceException($"{nameof(_clusterOptions.Authenticator)} should not be null");
        var certificateCallbackFactory = new CertificateValidationCallbackFactory(_clusterOptions, new Logger<CertificateValidationCallbackFactory>(_clusterOptions.Logging ?? new NullLoggerFactory()), _redactor);

        authenticator.AuthenticateHttpHandler(socketsHandler, clusterOptions, certificateCallbackFactory, _logger);

        var grpcChannelOptions = new GrpcChannelOptions()
        {
            LoggerFactory = clusterOptions.Logging,
            HttpHandler = socketsHandler,
        };

        GrpcChannel = GrpcChannel.ForAddress(_clusterOptions.ConnectionStringValue!.GetStellarBootstrapUri(), grpcChannelOptions);
        RetryHandler = new StellarRetryHandler();

        _bucketManager = new StellarBucketManager(this);
        _searchIndexManager = new StellarSearchIndexManager(this);
        _queryIndexManager = new StellarQueryIndexManager(this);
        _queryClient = new StellarQueryClient(this,
            new QueryService.QueryServiceClient(GrpcChannel),
            TypeSerializer,
            RetryHandler);
        _metaData = new Metadata();
        _analyticsClient = new StellarAnalyticsClient(this);
        _searchClient = new StellarSearchClient(this);

        authenticator.AuthenticateGrpcMetadata(_metaData);
    }

    public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        clusterOptions ??= new ClusterOptions();
        var opts = clusterOptions.WithConnectionString(connectionString);
        return await ConnectAsync(opts).ConfigureAwait(false);
    }

    public static async Task<ICluster> ConnectAsync(ClusterOptions clusterOptions)
    {
        var cluster = new StellarCluster(clusterOptions);
        await cluster.ConnectGrpcAsync(clusterOptions.KvConnectTimeout).ConfigureAwait(false);
        return cluster;
    }

    internal IRequestTracer RequestTracer { get; }
    internal ITypeTranscoder TypeTranscoder { get; }

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

    internal ITypeSerializer TypeSerializer { get; }

    public IServiceProvider ClusterServices =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Cluster Service Provider", "Protostellar");

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

    public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)=>
        throw ThrowHelper.ThrowFeatureNotAvailableException("WaitUntilReady", "Protostellar");

    public CallOptions GrpcCallOptions() => new (headers: _metaData);

    public CallOptions GrpcCallOptions(CancellationToken cancellationToken) => new (headers: _metaData, cancellationToken: cancellationToken);

    public CallOptions GrpcCallOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
        new (headers: _metaData, deadline: timeout.FromNow(), cancellationToken: cancellationToken);

    private IRequestSpan TraceSpan(string attr, IRequestSpan? parentSpan) =>
        this.RequestTracer.RequestSpan(attr, parentSpan);

    #region Bootstrapping/start up error propagation

    public Task BootStrapAsync() =>
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
}
#endif
