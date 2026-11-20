#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Admin.Query.V1;
using Couchbase.Management.Query;
using Couchbase.Stellar.Core.Retry;
using IndexType = Couchbase.Management.Views.IndexType;

namespace Couchbase.Stellar.Management.Query;

#nullable enable

internal class StellarQueryIndexManager : IQueryIndexManager
{
    private readonly StellarCluster _stellarCluster;
    private readonly QueryAdminService.QueryAdminServiceClient _stellarQueryAdminClient;
    private readonly IRetryOrchestrator _retryHandler;

    /// <summary>
    /// RFC 77: the span of each WaitForIndexOnline call inside WatchIndexes.
    /// </summary>
    internal const string WaitForIndexOnlineSpanName = "manager_query_wait_for_index_online";

    public StellarQueryIndexManager(StellarCluster stellarCluster)
    {
        _stellarQueryAdminClient = new QueryAdminService.QueryAdminServiceClient(stellarCluster.GrpcChannel);
        _stellarCluster = stellarCluster;
        _retryHandler = stellarCluster.RetryHandler;
    }

    public async Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(string bucketName,
        GetAllQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllQueryIndexOptions.DefaultReadOnly;

        var protoRequest = new GetAllIndexesRequest
        {
            BucketName = bucketName
        };
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.GetAllIndexes, OuterRequestSpans.ServiceSpan.N1QLQuery, "GetAllIndexes", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = true,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient
            .GetAllIndexesAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue)).ConfigureAwait(false);

        var response = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);

        //TODO: Should be ok using the null-coalescing since we're parsing Proto -> Core
        var coreIndexes = response.Indexes.Select(i => new QueryIndex
        {
            Name = i.Name,
            IsPrimary = i.IsPrimary,
            Type = Enum.Parse<IndexType>(i.Type.ToString(), true),
            State = i.State.ToString(),
            Keyspace = null,
            Partition = i.Partition ?? null,
            Condition = i.Condition ?? null,
            IndexKey = null,
            ScopeName = i.ScopeName,
            BucketName = i.BucketName
        });
        return coreIndexes;
    }

    public async Task CreateIndexAsync(string bucketName, string indexName, IEnumerable<string> indexKeys,
        CreateQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreateQueryIndexOptions.DefaultReadOnly;
        var protoRequest = new CreateIndexRequest
        {
            BucketName = bucketName,
            Name = indexName
        };
        if (opts.DeferredValue) protoRequest.Deferred = true;
        if (opts.IgnoreIfExistsValue) protoRequest.IgnoreIfExists = true;
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;
        protoRequest.Fields.AddRange(indexKeys);

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.CreateIndex, OuterRequestSpans.ServiceSpan.N1QLQuery, "CreateIndex", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient.CreateIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue))
            .ConfigureAwait(false);

        _ = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);
    }

    public async Task CreatePrimaryIndexAsync(string bucketName, CreatePrimaryQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreatePrimaryQueryIndexOptions.DefaultReadOnly;
        var protoRequest = new CreatePrimaryIndexRequest
        {
            BucketName = bucketName
        };
        if (opts.DeferredValue) protoRequest.Deferred = true;
        if (opts.IgnoreIfExistsValue) protoRequest.IgnoreIfExists = true;
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;
        if (opts.IndexNameValue != null) protoRequest.Name = opts.IndexNameValue;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.CreatePrimaryIndex, OuterRequestSpans.ServiceSpan.N1QLQuery, "CreatePrimaryIndex", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient
            .CreatePrimaryIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue)).ConfigureAwait(false);
        _ = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);
    }

    public async Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropQueryIndexOptions.DefaultReadOnly;
        var protoRequest = new DropIndexRequest
        {
            BucketName = bucketName,
            Name = indexName
        };
        if (opts.IgnoreIfExistsValue) protoRequest.IgnoreIfMissing = true;
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.DropIndex, OuterRequestSpans.ServiceSpan.N1QLQuery, "DropIndex", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient.DropIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue))
            .ConfigureAwait(false);

        _ = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);
    }

    public async Task DropPrimaryIndexAsync(string bucketName, DropPrimaryQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropPrimaryQueryIndexOptions.DefaultReadOnly;
        var protoRequest = new DropPrimaryIndexRequest
        {
            BucketName = bucketName
        };
        if (opts.IgnoreIfExistsValue) protoRequest.IgnoreIfMissing = true;
        if (opts.IndexNameValue != null) protoRequest.Name = opts.IndexNameValue;
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.DropPrimaryIndex, OuterRequestSpans.ServiceSpan.N1QLQuery, "DropPrimaryIndex", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient.DropPrimaryIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue))
            .ConfigureAwait(false);

        _ = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);
    }

    public async Task BuildDeferredIndexesAsync(string bucketName, BuildDeferredQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? BuildDeferredQueryIndexOptions.DefaultReadOnly;
        var protoRequest = new BuildDeferredIndexesRequest
        {
            BucketName = bucketName,
        };
        if (opts.ScopeNameValue != null) protoRequest.ScopeName = opts.ScopeNameValue;
        if (opts.CollectionNameValue != null) protoRequest.CollectionName = opts.CollectionNameValue;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.BuildDeferredIndexes, OuterRequestSpans.ServiceSpan.N1QLQuery, "BuildDeferredIndexes", options?.RequestSpanValue);
        var request = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.TokenValue,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var grpcCall = async () => await _stellarQueryAdminClient.BuildDeferredIndexesAsync(protoRequest, _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue))
            .ConfigureAwait(false);

        _ = await _retryHandler.RetryAsync(grpcCall, request).ConfigureAwait(false);
    }

    public async Task WatchIndexesAsync(string bucketName, IEnumerable<string> indexNames,
        WatchQueryIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? WatchQueryIndexOptions.DefaultReadOnly;

        // RFC 77: WatchIndexes is a composite command. Its span has no db.operation, and it has one
        // manager_query_wait_for_index_online span (db.operation WaitForIndexOnline) for each index.
        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Query.WatchIndexes,
            OuterRequestSpans.ServiceSpan.N1QLQuery, null, options?.RequestSpanValue);
        var requests = new List<StellarRequest>();
        try
        {
            await Task.WhenAll(indexNames.Select(WaitForIndexOnlineAsync)).ConfigureAwait(false);
            span.SetStatus(RequestSpanStatusCode.Ok);
        }
        catch
        {
            span.SetStatus(RequestSpanStatusCode.Error);
            throw;
        }
        finally
        {
            uint retries = 0;
            lock (requests)
            {
                foreach (var request in requests) retries += request.Attempts;
            }
            span.SetAttribute(OuterRequestSpans.Attributes.Retries, retries);
        }

        async Task<WaitForIndexOnlineResponse> WaitForIndexOnlineAsync(string name)
        {
            var protoRequest = new WaitForIndexOnlineRequest
            {
                BucketName = bucketName, Name = name,
                CollectionName = opts.CollectionNameValue ?? "_default",
                ScopeName = opts.ScopeNameValue ?? "_default"
            };
            using var indexSpan = _stellarCluster.ManagerSpan(WaitForIndexOnlineSpanName,
                OuterRequestSpans.ServiceSpan.N1QLQuery, "WaitForIndexOnline", span);
            var request = new StellarRequest
            {
                Span = indexSpan,
                Idempotent = false,
                Token = opts.TokenValue,
                Timeout = opts.TimeoutValue
            };
            lock (requests)
            {
                requests.Add(request);
            }

            async Task<WaitForIndexOnlineResponse> GrpcCall() =>
                await _stellarQueryAdminClient.WaitForIndexOnlineAsync(protoRequest,
                        _stellarCluster.GrpcCallOptions(request.RemainingTimeout, opts.TokenValue))
                    .ConfigureAwait(false);

            return await _retryHandler.RetryAsync((Func<Task<WaitForIndexOnlineResponse>>?)GrpcCall,
                request).ConfigureAwait(false);
        }
    }
}
#endif
