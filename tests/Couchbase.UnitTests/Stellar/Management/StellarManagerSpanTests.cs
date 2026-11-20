#if NETCOREAPP3_1_OR_GREATER
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Query;
using Couchbase.Stellar;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Management.Buckets;
using Couchbase.Stellar.Management.Collections;
using Couchbase.Stellar.Management.Query;
using Couchbase.Stellar.Management.Search;
using Couchbase.Stellar.Search;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Management;

/// <summary>
/// RFC 77 "Tracing in the management APIs": each couchbase2 management operation has its RFC 67
/// span, with db.operation set to the Protostellar RPC name (NCBC-4429).
/// </summary>
public class StellarManagerSpanTests
{
    [Theory]
    [InlineData("CreateBucket", "manager_buckets_create_bucket", "management", "CreateBucket")]
    [InlineData("UpdateBucket", "manager_buckets_update_bucket", "management", "UpdateBucket")]
    [InlineData("DropBucket", "manager_buckets_drop_bucket", "management", "DeleteBucket")]
    [InlineData("GetAllBuckets", "manager_buckets_get_all_buckets", "management", "ListBuckets")]
    [InlineData("GetBucket", "manager_buckets_get_bucket", "management", "ListBuckets")]
    [InlineData("GetAllScopes", "manager_collections_get_all_scopes", "management", "ListCollections")]
    [InlineData("CreateCollection", "manager_collections_create_collection", "management", "CreateCollection")]
    [InlineData("DropCollection", "manager_collections_drop_collection", "management", "DeleteCollection")]
    [InlineData("CreateScope", "manager_collections_create_scope", "management", "CreateScope")]
    [InlineData("DropScope", "manager_collections_drop_scope", "management", "DeleteScope")]
    [InlineData("Search.GetIndex", "manager_search_get_index", "search", "GetIndex")]
    [InlineData("Search.GetAllIndexes", "manager_search_get_all_indexes", "search", "ListIndexes")]
    [InlineData("Search.UpsertIndex.Create", "manager_search_upsert_index", "search", "CreateIndex")]
    [InlineData("Search.DropIndex", "manager_search_drop_index", "search", "DeleteIndex")]
    [InlineData("Search.GetIndexedDocumentsCount", "manager_search_get_indexed_documents_count", "search", "GetIndexedDocumentsCount")]
    [InlineData("Search.PauseIngest", "manager_search_pause_ingest", "search", "PauseIndexIngest")]
    [InlineData("Search.ResumeIngest", "manager_search_resume_ingest", "search", "ResumeIndexIngest")]
    [InlineData("Search.AllowQuerying", "manager_search_allow_querying", "search", "AllowIndexQuerying")]
    [InlineData("Search.DisallowQuerying", "manager_search_disallow_querying", "search", "DisallowIndexQuerying")]
    [InlineData("Search.FreezePlan", "manager_search_freeze_plan", "search", "FreezeIndexPlan")]
    [InlineData("Search.UnfreezePlan", "manager_search_unfreeze_plan", "search", "UnfreezeIndexPlan")]
    [InlineData("Query.GetAllIndexes", "manager_query_get_all_indexes", "query", "GetAllIndexes")]
    [InlineData("Query.CreateIndex", "manager_query_create_index", "query", "CreateIndex")]
    [InlineData("Query.CreatePrimaryIndex", "manager_query_create_primary_index", "query", "CreatePrimaryIndex")]
    [InlineData("Query.DropIndex", "manager_query_drop_index", "query", "DropIndex")]
    [InlineData("Query.DropPrimaryIndex", "manager_query_drop_primary_index", "query", "DropPrimaryIndex")]
    [InlineData("Query.BuildDeferredIndexes", "manager_query_build_deferred_indexes", "query", "BuildDeferredIndexes")]
    public async Task ManagementOperation_HasOneSpan_WithProtostellarOperation(string operation, string spanName,
        string service, string protostellarOperation)
    {
        var (cluster, tracer, orchestrator) = CreateCluster();

        // The fake orchestrator returns an empty response, so mapping it may throw; only the span matters.
        var exception = await Record.ExceptionAsync(() => Invoke(cluster, operation));

        Assert.True(tracer.Spans.Count == 1, $"{tracer.Spans.Count} spans; exception: {exception}");
        var span = tracer.Spans[0];
        Assert.Equal(spanName, span.Name);
        Assert.Equal("couchbase", span.Attributes[OuterRequestSpans.Attributes.System.Key]);
        Assert.Equal(service, span.Attributes[OuterRequestSpans.Attributes.Service]);
        Assert.Equal(protostellarOperation, span.Attributes[OuterRequestSpans.Attributes.Operation]);
        Assert.Equal(1u, span.Attributes[OuterRequestSpans.Attributes.Retries]);
        Assert.True(span.Ended);

        var request = Assert.Single(orchestrator.Requests);
        Assert.Same(span, request.Span);
    }

    [Fact]
    public async Task ManagementOperation_UsesParentSpanFromOptions()
    {
        var (cluster, tracer, _) = CreateCluster();
        var outer = tracer.RequestSpan("outer");

        await new StellarBucketManager(cluster).DropBucketAsync("b", new DropBucketOptions().RequestSpan(outer));

        var span = Assert.Single(tracer.Spans, s => s.Name == "manager_buckets_drop_bucket");
        Assert.Same(outer, span.Parent);
    }

    [Fact]
    public async Task WatchIndexes_HasCompositeSpan_WithOneWaitForIndexOnlineSpanPerIndex()
    {
        var (cluster, tracer, orchestrator) = CreateCluster();
        var outer = tracer.RequestSpan("outer");

        await new StellarQueryIndexManager(cluster).WatchIndexesAsync("b", new[] { "idx1", "idx2" },
            new WatchQueryIndexOptions().RequestSpan(outer));

        var watch = Assert.Single(tracer.Spans, s => s.Name == "manager_query_watch_indexes");
        Assert.Same(outer, watch.Parent);
        Assert.Equal("query", watch.Attributes[OuterRequestSpans.Attributes.Service]);
        Assert.False(watch.Attributes.ContainsKey(OuterRequestSpans.Attributes.Operation));
        Assert.Equal(2u, watch.Attributes[OuterRequestSpans.Attributes.Retries]);
        Assert.Equal(RequestSpanStatusCode.Ok, watch.Status);
        Assert.True(watch.Ended);

        var waits = tracer.Spans.Where(s => s.Name == "manager_query_wait_for_index_online").ToList();
        Assert.Equal(2, waits.Count);
        Assert.All(waits, w =>
        {
            Assert.Same(watch, w.Parent);
            Assert.Equal("query", w.Attributes[OuterRequestSpans.Attributes.Service]);
            Assert.Equal("WaitForIndexOnline", w.Attributes[OuterRequestSpans.Attributes.Operation]);
            Assert.Equal(1u, w.Attributes[OuterRequestSpans.Attributes.Retries]);
            Assert.True(w.Ended);
        });
        Assert.True(waits.Cast<IRequestSpan>().ToHashSet().SetEquals(orchestrator.Requests.Select(r => r.Span!)));
    }

    private static Task Invoke(StellarCluster cluster, string operation)
    {
        var buckets = new StellarBucketManager(cluster);
        var collections = new StellarCollectionManager(cluster, "b");
        var search = new StellarSearchIndexManager(cluster);
        var query = new StellarQueryIndexManager(cluster);
        var settings = new BucketSettings { Name = "b" };
        var searchIndex = new SearchIndex { Name = "i", Type = "fulltext-index", SourceName = "b", SourceType = "couchbase", SourceUuid = "su",
            Params = new(), SourceParams = new(), PlanParams = new() };

        return operation switch
        {
            "CreateBucket" => buckets.CreateBucketAsync(settings),
            "UpdateBucket" => buckets.UpdateBucketAsync(settings),
            "DropBucket" => buckets.DropBucketAsync("b"),
            "GetAllBuckets" => buckets.GetAllBucketsAsync(),
            "GetBucket" => buckets.GetBucketAsync("b"),
            "GetAllScopes" => collections.GetAllScopesAsync(),
            "CreateCollection" => collections.CreateCollectionAsync("s", "c", new CreateCollectionSettings()),
            "DropCollection" => collections.DropCollectionAsync("s", "c"),
            "CreateScope" => collections.CreateScopeAsync("s"),
            "DropScope" => collections.DropScopeAsync("s"),
            "Search.GetIndex" => search.GetIndexAsync("i"),
            "Search.GetAllIndexes" => search.GetAllIndexesAsync(),
            "Search.UpsertIndex.Create" => search.UpsertIndexAsync(searchIndex),
            "Search.DropIndex" => search.DropIndexAsync("i"),
            "Search.GetIndexedDocumentsCount" => search.GetIndexedDocumentsCountAsync("i"),
            "Search.PauseIngest" => search.PauseIngestAsync("i"),
            "Search.ResumeIngest" => search.ResumeIngestAsync("i"),
            "Search.AllowQuerying" => search.AllowQueryingAsync("i"),
            "Search.DisallowQuerying" => search.DisallowQueryingAsync("i"),
            "Search.FreezePlan" => search.FreezePlanAsync("i"),
            "Search.UnfreezePlan" => search.UnfreezePlanAsync("i"),
            "Query.GetAllIndexes" => query.GetAllIndexesAsync("b"),
            "Query.CreateIndex" => query.CreateIndexAsync("b", "i", new[] { "f" }),
            "Query.CreatePrimaryIndex" => query.CreatePrimaryIndexAsync("b"),
            "Query.DropIndex" => query.DropIndexAsync("b", "i"),
            "Query.DropPrimaryIndex" => query.DropPrimaryIndexAsync("b"),
            "Query.BuildDeferredIndexes" => query.BuildDeferredIndexesAsync("b"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static (StellarCluster, RecordingTracer, FakeRetryOrchestrator) CreateCluster()
    {
        var tracer = new RecordingTracer();
        var orchestrator = new FakeRetryOrchestrator();
        var cluster = new StellarCluster(new Mock<IBucketManager>().Object, new Mock<ISearchIndexManager>().Object,
            new Mock<IQueryIndexManager>().Object, new Mock<IQueryClient>().Object,
            new Mock<IAnalyticsClient>().Object, new Mock<IStellarSearchClient>().Object, new Metadata(),
            tracer, GrpcChannel.ForAddress(new Uri("https://xxx")), new Mock<ITypeSerializer>().Object,
            orchestrator, new ClusterOptions(), new Mock<IOperationCompressor>().Object);
        return (cluster, tracer, orchestrator);
    }

    /// <summary>
    /// Does what StellarRetryHandler does around one successful attempt, without calling gRPC.
    /// </summary>
    private sealed class FakeRetryOrchestrator : IRetryOrchestrator
    {
        public ConcurrentBag<StellarRequest> Requests { get; } = new();

        public Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult
        {
            var stellarRequest = (StellarRequest)request;
            Requests.Add(stellarRequest);
            stellarRequest.Attempts = 1;
            stellarRequest.StopRecording();
            return Task.FromResult(Activator.CreateInstance<T>());
        }

        public Task<ResponseStatus> RetryAsync(BucketBase bucket, IOperation operation,
            CancellationTokenPair tokenPair = default) => throw new NotSupportedException();
    }

    private sealed class RecordingTracer : IRequestTracer
    {
        private readonly ConcurrentQueue<RecordingSpan> _spans = new();

        /// <summary>Spans started by the code under test (not the "outer" span the test made).</summary>
        public List<RecordingSpan> Spans => _spans.Where(s => s.Name != "outer").ToList();

        public IRequestSpan RequestSpan(string name, IRequestSpan? parentSpan = null)
        {
            var span = new RecordingSpan(this, name, parentSpan);
            _spans.Enqueue(span);
            return span;
        }

        public IRequestTracer Start(TraceListener listener) => this;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingSpan(RecordingTracer tracer, string name, IRequestSpan? parent) : IRequestSpan
    {
        public string Name { get; } = name;
        public ConcurrentDictionary<string, object> Attributes { get; } = new();
        public bool Ended { get; private set; }
        public RequestSpanStatusCode Status { get; private set; }
        public IRequestSpan? Parent { get; set; } = parent;
        public bool CanWrite => true;
        public string? Id { get; } = Guid.NewGuid().ToString();
        public uint? Duration => null;
        public IRequestSpan SetAttribute(string key, bool value) { Attributes[key] = value; return this; }
        public IRequestSpan SetAttribute(string key, string value) { Attributes[key] = value; return this; }
        public IRequestSpan SetAttribute(string key, uint value) { Attributes[key] = value; return this; }
        public IRequestSpan AddEvent(string eventName, DateTimeOffset? timestamp = null) => this;
        public IRequestSpan SetStatus(RequestSpanStatusCode code) { Status = code; return this; }
        public IRequestSpan ChildSpan(string childName) => tracer.RequestSpan(childName, this);
        public void End() => Dispose();
        public void Dispose() => Ended = true;
    }
}
#endif
