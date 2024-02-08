#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Diagnostics;
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
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Management.Buckets;
using Couchbase.Stellar.Management.Query;
using Couchbase.Stellar.Management.Search;
using Couchbase.Stellar.Query;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Couchbase.Utils;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace Couchbase.Stellar;

#nullable enable

internal class StellarCluster : ICluster, IBootstrappable, IClusterExtended
{
    private readonly ClusterOptions _clusterOptions;
    private readonly List<Exception> _deferredExceptions = new();
    private readonly IBucketManager _bucketManager;
    private readonly ISearchIndexManager _searchIndexManager;
    private readonly IQueryIndexManager _queryIndexManager;
    private readonly QueryService.QueryServiceClient _queryClient;
    private readonly IAnalyticsClient _analyticsClient;
    private readonly IStellarSearchClient _searchClient;
    private readonly Metadata _metaData;
    private readonly ConcurrentDictionary<string, IBucket> _buckets = new();
    private ClusterChannelCredentials ChannelCredentials { get; }

    internal StellarCluster(IBucketManager bucketManager, ISearchIndexManager searchIndexManager,
        IQueryIndexManager queryIndexManager, QueryService.QueryServiceClient queryClient,
        IAnalyticsClient analyticsClient, IStellarSearchClient searchClient,
        Metadata metaData, ClusterChannelCredentials channelCredentials, IRequestTracer requestTracer, GrpcChannel grpcChannel,
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
        ChannelCredentials = channelCredentials;
        RequestTracer = requestTracer;
        GrpcChannel = grpcChannel;
        TypeSerializer = typeSerializer;
        RetryHandler = retryHandler;
    }

    private StellarCluster(ClusterOptions clusterOptions)
    {
        _clusterOptions = clusterOptions;
        RequestTracer = clusterOptions.TracingOptions.RequestTracer;
        TypeSerializer = clusterOptions.Serializer ?? SystemTextJsonSerializer.Create();
        ChannelCredentials = new ClusterChannelCredentials(clusterOptions);
        var socketsHandler = new SocketsHttpHandler();
        var serverCertValidationCallback = clusterOptions.HttpCertificateCallbackValidation ??
                                           clusterOptions.KvCertificateCallbackValidation;
        var ignoreNameMismatch = clusterOptions.HttpIgnoreRemoteCertificateMismatch ||
                                 clusterOptions.KvIgnoreRemoteCertificateNameMismatch;

        if (serverCertValidationCallback is not null && ignoreNameMismatch)
        {
            // combine the checks.
            var existingCallback = serverCertValidationCallback;
            serverCertValidationCallback = (sender, certificate, chain, errors) =>
            {
                errors = CertificateFactory.WithoutNameMismatch(errors);
                return existingCallback(sender, certificate, chain, errors);
            };
        }
        else if (ignoreNameMismatch)
        {
            serverCertValidationCallback = (_, _, _, errors) =>
            {
                var sslPolicyErrors = CertificateFactory.WithoutNameMismatch(errors);
                return sslPolicyErrors == SslPolicyErrors.None;
            };
        }

        if (serverCertValidationCallback is not null)
        {
            socketsHandler.SslOptions = new SslClientAuthenticationOptions()
            {
                RemoteCertificateValidationCallback = serverCertValidationCallback
            };
        }

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
        _queryClient = new Protostellar.Query.V1.QueryService.QueryServiceClient(GrpcChannel);
        _metaData = new Metadata();
        _analyticsClient = new StellarAnalyticsClient(this);
        _searchClient = new StellarSearchClient(this);

        if (ChannelCredentials.BasicAuthHeader != null)
        {
            _metaData.Add("Authorization", ChannelCredentials.BasicAuthHeader);
        }
    }

    public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        clusterOptions ??= new ClusterOptions();
        var opts = clusterOptions.WithConnectionString(connectionString);
        return await ConnectAsync(opts).ConfigureAwait(false);
    }

    public static async Task<ICluster> ConnectAsync(ClusterOptions clusterOptions)
    {
        var clusterWrapper = new StellarCluster(clusterOptions);
        await clusterWrapper.ConnectGrpcAsync(clusterOptions.KvConnectTimeout).ConfigureAwait(false);
        return clusterWrapper;
    }

    internal IRequestTracer RequestTracer { get; }

    private async Task ConnectGrpcAsync(TimeSpan kvConnectTimeout)
    {
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

    public IServiceProvider ClusterServices => throw new UnsupportedInProtostellarException("Cluster Service Provider");

    public IQueryIndexManager QueryIndexes => _queryIndexManager;

    public IAnalyticsIndexManager AnalyticsIndexes => throw new UnsupportedInProtostellarException("Analytics Index Management");

    public ISearchIndexManager SearchIndexes => _searchIndexManager;

    public IBucketManager Buckets => _bucketManager;

    public IUserManager Users => throw new UnsupportedInProtostellarException("User Management");

    public IEventingFunctionManager EventingFunctions => throw new UnsupportedInProtostellarException("Eventing Functions");

    public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = null)
    {
        ThrowIfBootStrapFailed();

        options ??= new AnalyticsOptions();
        return await _analyticsClient.QueryAsync<T>(statement, options).ConfigureAwait(false);
    }

    public ValueTask<IBucket> BucketAsync(string name)
    {
        return new ValueTask<IBucket>(_buckets.GetOrAdd(name, new StellarBucket(name, this, _queryClient)));
    }

    public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Diagnostics");
    }

    public void Dispose()
    {
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

    public Task<IPingReport> PingAsync(PingOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Ping");
    }

    public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
    {
        ThrowIfBootStrapFailed();

        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;
        return QueryAsync<T>(statement, opts);
    }

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions.ReadOnlyRecord opts)
    {
        ThrowIfBootStrapFailed();

        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.N1QLQuery, opts.RequestSpan);

        var request = new QueryRequest
        {
            Statement = statement,
            ReadOnly = opts.ReadOnly ?? false,
            Prepared = opts.IsPrepared,
            ScanConsistency = opts.ScanConsistency.ToProto(),
            FlexIndex = opts.FlexIndex,
            PreserveExpiry = opts.PreserveExpiry,
        };

        // because of the way the GRPC library handles Optional, we have to use if statements rather than the '??=' operator
        // setting the non-nullable SomeProperty sets the associated HasSomeProperty bool.
        if (opts.BucketName != null) request.BucketName = opts.BucketName;
        if (opts.ScopeName != null) request.ScopeName = opts.ScopeName;
        if (opts.CurrentContextId != null) request.ClientContextId = opts.CurrentContextId;

        var tuningOptions = new QueryRequest.Types.TuningOptions();
        if (opts.MaxServerParallelism.HasValue) tuningOptions.MaxParallelism = (uint)opts.MaxServerParallelism.Value;
        if (opts.PipelineBatch.HasValue) tuningOptions.PipelineBatch = (uint)opts.PipelineBatch.Value;
        if (opts.PipelineCapacity.HasValue) tuningOptions.PipelineCap = (uint)opts.PipelineCapacity.Value;
        if (opts.ScanWait.HasValue) tuningOptions.ScanWait = Duration.FromTimeSpan(opts.ScanWait.Value);
        if (opts.ScanCapacity.HasValue) tuningOptions.ScanCap = (uint)opts.ScanCapacity.Value;
        if (opts.IncludeMetrics == false) tuningOptions.DisableMetrics = true;
        request.TuningOptions = tuningOptions;
        request.ProfileMode = opts.Profile.ToProto();

        var callOptions = GrpcCallOptions(opts.TimeOut, opts.Token);
        var asyncResponse = _queryClient.Query(request, callOptions);
        var headers = await asyncResponse.ResponseHeadersAsync.ConfigureAwait(false);
        var streamingResult = new StellarQueryResult<T>(asyncResponse, TypeSerializer);

        return streamingResult;
    }

    public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = null)
    {
        ThrowIfBootStrapFailed();

        return await _searchClient.QueryAsync(indexName, query, options).ConfigureAwait(false);
    }

    public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Wait Until Ready");
    }
    public Grpc.Core.CallOptions GrpcCallOptions() => new (headers: _metaData);
    public Grpc.Core.CallOptions GrpcCallOptions(CancellationToken cancellationToken) => new (headers: _metaData, cancellationToken: cancellationToken);
    public Grpc.Core.CallOptions GrpcCallOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
        new (headers: _metaData, deadline: timeout.FromNow(), cancellationToken: cancellationToken);

    private IRequestSpan TraceSpan(string attr, IRequestSpan? parentSpan) =>
        this.RequestTracer.RequestSpan(attr, parentSpan);

    #region Bootstrapping/start up error propagation

    public Task BootStrapAsync()
    {
        throw new UnsupportedInProtostellarException("Boot Strap");
    }

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
