using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Diagnostics;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;
using Grpc.Net.Client;
using System.Net.Security;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar.CouchbaseClient.Admin;
using Couchbase.Stellar.Util;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Couchbase.Stellar.CouchbaseClient
{
    internal class ProtoCluster : ICluster
    {
        private readonly string _connectionString;
        private readonly ProtoBucketManager _bucketManager;
        private readonly QueryService.QueryServiceClient _queryClient;
        private readonly ProtoAnalyticsClient _analyticsClient;
        private readonly ProtoSearchClient _searchClient;
        private readonly Metadata _metaData;
        internal ClusterChannelCredentials ChannelCredentials { get; }


        internal ProtoCluster(ClusterOptions clusterOptions)
        {
            _connectionString = clusterOptions.ConnectionString ?? throw new ArgumentNullException(nameof(clusterOptions.ConnectionString));
            var uriBuilder = new UriBuilder(_connectionString);
            var enableTls = clusterOptions.EnableTls != false;
            uriBuilder.Scheme = enableTls == true ? "https" : "http";
            if (uriBuilder.Port <= 0)
            {
                // FIXME: Is this only for the dev gateway?
                uriBuilder.Port = 18098;
            }

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

            GrpcChannel = GrpcChannel.ForAddress(uriBuilder.Uri, grpcChannelOptions);

            _bucketManager = new ProtoBucketManager(GrpcChannel);
            _queryClient = new QueryService.QueryServiceClient(GrpcChannel);
            _metaData = new Metadata();
            _analyticsClient = new ProtoAnalyticsClient(this);
            _searchClient = new ProtoSearchClient(this);

            if (this.ChannelCredentials.BasicAuthHeader != null)
            {
                _metaData.Add("Authorization", this.ChannelCredentials.BasicAuthHeader);
            }
        }

        internal IRequestTracer RequestTracer { get; }

        internal Task ConnectGrpcAsync(CancellationToken cancellationToken) => GrpcChannel.ConnectAsync(cancellationToken);

        internal GrpcChannel GrpcChannel { get; }
        internal ITypeSerializer TypeSerializer { get; }


        public IServiceProvider ClusterServices => throw new NotImplementedException();

        public IQueryIndexManager QueryIndexes => throw new NotImplementedException();

        public IAnalyticsIndexManager AnalyticsIndexes => throw new NotImplementedException();

        public ISearchIndexManager SearchIndexes => throw new NotImplementedException();

        public IBucketManager Buckets => _bucketManager;

        public IUserManager Users => throw new NotImplementedException();

        public IEventingFunctionManager EventingFunctions => throw new NotImplementedException();

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = null)
        {
            var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

            var analyticsRequest = new AnalyticsQueryRequest
            {
                Statement = statement,
                ReadOnly = opts.Readonly,
                Priority = opts.Priority == -1
            };
            if (opts.BucketName != null) analyticsRequest.BucketName = opts.BucketName;
            if (opts.ScopeName != null) analyticsRequest.ScopeName = opts.ScopeName;
            if (opts.ClientContextId != null) analyticsRequest.ReadOnly = opts.Readonly;

            return await _analyticsClient.QueryAsync<T>(analyticsRequest, options).ConfigureAwait(false);
        }
        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, string bucketName, string scopeName, AnalyticsOptions? options = null)
        {
            var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

            var analyticsRequest = new AnalyticsQueryRequest
            {
                BucketName = bucketName,
                ScopeName = scopeName,
                Statement = statement,
                ReadOnly = opts.Readonly,
                Priority = opts.Priority == -1
            };
            if (opts.ClientContextId != null) analyticsRequest.ReadOnly = opts.Readonly;

            return await _analyticsClient.QueryAsync<T>(analyticsRequest, options).ConfigureAwait(false);
        }

        public ValueTask<IBucket> BucketAsync(string name) => new ValueTask<IBucket>(new ProtoBucket(name, this, _queryClient));

        public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            throw new NotImplementedException();
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

            // in protostellar, this is a one-of enum, not a Flags.
            var profile = opts.Profile;
            if (profile.HasFlag(QueryProfile.Off))
                request.ProfileMode = QueryRequest.Types.ProfileMode.Off;
            else if (profile.HasFlag(QueryProfile.Phases))
                request.ProfileMode = QueryRequest.Types.ProfileMode.Phases;
            else if (profile.HasFlag(QueryProfile.Timings))
                request.ProfileMode = QueryRequest.Types.ProfileMode.Timings;

            var callOptions = GrpcCallOptions(opts.TimeOut, opts.Token);
            var asyncResponse = _queryClient.Query(request, callOptions);
            var headers = await asyncResponse.ResponseHeadersAsync.ConfigureAwait(false);
            var streamingResult = new ProtoQueryResult<T>(asyncResponse, TypeSerializer);
            return streamingResult;
        }

        public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = null)
        {
            return await _searchClient.QueryAsync(indexName, query, options).ConfigureAwait(false);
        }

        public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
        {
            throw new NotImplementedException();
        }
        public Grpc.Core.CallOptions GrpcCallOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
            new (headers: _metaData, deadline: timeout.FromNow(), cancellationToken: cancellationToken);

        private IRequestSpan TraceSpan(string attr, IRequestSpan? parentSpan) =>
            this.RequestTracer.RequestSpan(attr, parentSpan);
    }
}
