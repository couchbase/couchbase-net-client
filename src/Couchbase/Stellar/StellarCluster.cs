#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Serializers;
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
using Couchbase.Stellar.Management.Buckets;
using Couchbase.Stellar.Management.Query;
using Couchbase.Stellar.Management.Search;
using Couchbase.Stellar.Query;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace Couchbase.Stellar;

#nullable enable

public class StellarCluster : ICluster //TODO: To change back to internal later
{
    private readonly ClusterOptions _clusterOptions;
    private readonly StellarBucketManager _bucketManager;
    private readonly StellarSearchIndexManager _searchIndexManager;
    private readonly StellarQueryIndexManager _queryIndexManager;
    private readonly QueryService.QueryServiceClient _queryClient;
    private readonly ProtoAnalyticsClient _analyticsClient;
    private readonly StellarSearchClient _searchClient;
    private readonly Metadata _metaData;
    private ClusterChannelCredentials ChannelCredentials { get; }


    internal StellarCluster(ClusterOptions clusterOptions)
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

        _bucketManager = new StellarBucketManager(this);
        _searchIndexManager = new StellarSearchIndexManager(this);
        _queryIndexManager = new StellarQueryIndexManager(this);
        _queryClient = new Protostellar.Query.V1.QueryService.QueryServiceClient(GrpcChannel);
        _metaData = new Metadata();
        _analyticsClient = new ProtoAnalyticsClient(this);
        _searchClient = new StellarSearchClient(this);

        if (this.ChannelCredentials.BasicAuthHeader != null)
        {
            _metaData.Add("Authorization", this.ChannelCredentials.BasicAuthHeader);
        }
    }

    public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? clusterOptions = null)
    {
        clusterOptions ??= new ClusterOptions();
        var opts = clusterOptions.WithConnectionString(connectionString);
        return await ConnectAsync(opts).ConfigureAwait(false);
    }

    public static async Task<ICluster> ConnectAsync(ClusterOptions? clusterOptions = null)
    {
        if (!Uri.TryCreate(clusterOptions?.ConnectionString, UriKind.Absolute, out var parsedUri))
        {
            throw new ArgumentOutOfRangeException(nameof(clusterOptions.ConnectionString));
        }

        var clusterWrapper = new StellarCluster(clusterOptions);
        using var cts = new CancellationTokenSource(clusterOptions.KvConnectTimeout);
        try
        {
            await clusterWrapper.ConnectGrpcAsync(cts.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new ConnectException($"Could not connect to {parsedUri} after 10 seconds.");
        }

        return clusterWrapper;
    }

    internal IRequestTracer RequestTracer { get; }

    internal Task ConnectGrpcAsync(CancellationToken cancellationToken) => GrpcChannel.ConnectAsync(cancellationToken);

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
        var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

        var request = new AnalyticsQueryRequest
        {
            Statement = statement,
            ReadOnly = opts.Readonly,
            Priority = opts.Priority == -1
        };
        if (opts.BucketName != null) request.BucketName = opts.BucketName;
        if (opts.ScopeName != null) request.ScopeName = opts.ScopeName;
        if (opts.ClientContextId != null) request.ReadOnly = opts.Readonly;

        return await _analyticsClient.QueryAsync<T>(request, options).ConfigureAwait(false);
    }
    public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, string bucketName, string scopeName, AnalyticsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

        var request = new AnalyticsQueryRequest
        {
            BucketName = bucketName,
            ScopeName = scopeName,
            Statement = statement,
            ReadOnly = opts.Readonly,
            Priority = opts.Priority == -1
        };
        if (opts.ClientContextId != null) request.ReadOnly = opts.Readonly;

        return await _analyticsClient.QueryAsync<T>(request, options).ConfigureAwait(false);
    }

    public ValueTask<IBucket> BucketAsync(string name) => new ValueTask<IBucket>(new StellarBucket(name, this, _queryClient));

    public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Diagnostics");
    }

    public void Dispose()
    {
        GrpcChannel.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        throw new UnsupportedInProtostellarException("Async Dispose");
    }

    public Task<IPingReport> PingAsync(PingOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Ping");
    }

    public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;
        return QueryAsync<T>(statement, opts);
    }

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions.ReadOnlyRecord opts)
    {
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
}
#endif
