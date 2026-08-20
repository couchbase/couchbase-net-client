using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// Every key/value operation must resolve the collection ID before it dispatches. If it does
    /// not, and the connection negotiated collections in HELO, the server reads the first byte of
    /// the document key as a leb128 collection ID and the operation silently addresses the wrong
    /// collection: NCBC-4285, where GetPrimary was a near-identical copy of GetReplica that had
    /// never been given the guard.
    ///
    /// The point of these tests is <see cref="Every_public_operation_is_covered"/>. A table of
    /// operations only proves something about the operations someone remembered to add, so the
    /// reflection test fails when the public surface grows past the table.
    /// </summary>
    public class CouchbaseCollectionCollectionIdTests
    {
        private const string DocId = "thekey";

        /// <summary>The collection ID encoded in <see cref="GetCidResponse"/>.</summary>
        private const uint FakeCid = 0x17;

        /// <summary>
        /// A real GET_CID success response, taken from
        /// <c>CollectionOperationTests.Test_GetCid_WithValue</c>. Serving a genuine packet means the
        /// collection actually ends up holding a CID, so the tests can assert on the value that
        /// reached the wire rather than merely that a fetch was attempted.
        /// </summary>
        private static readonly byte[] GetCidResponse =
        [
            0x18, 0xbb, 0x03, 0x00, 0x0c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x1a,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x1f, 0x00, 0x00, 0x00, 0x17
        ];

        private static readonly LookupInSpec[] LookupSpecs = [LookupInSpec.Get("name")];
        private static readonly MutateInSpec[] MutateSpecs = [MutateInSpec.Upsert("name", "mike")];

        /// <summary>
        /// One entry per public operation. Keys are labels rather than bare method names so the two
        /// UnlockAsync overloads can both appear; <see cref="Every_public_operation_is_covered"/>
        /// matches on the part before any generic marker.
        /// </summary>
        private static readonly Dictionary<string, Func<CouchbaseCollection, Task>> Operations = new()
        {
            ["GetAsync"] = c => c.GetAsync(DocId),
            ["ExistsAsync"] = c => c.ExistsAsync(DocId),
            ["UpsertAsync"] = c => c.UpsertAsync(DocId, new { name = "mike" }),
            ["InsertAsync"] = c => c.InsertAsync(DocId, new { name = "mike" }),
            ["ReplaceAsync"] = c => c.ReplaceAsync(DocId, new { name = "mike" }),
            ["RemoveAsync"] = c => c.RemoveAsync(DocId),
            ["UnlockAsync"] = c => c.UnlockAsync(DocId, 0UL),
            ["UnlockAsync<T>"] = c => c.UnlockAsync<object>(DocId, 0UL),
            ["TouchAsync"] = c => c.TouchAsync(DocId, TimeSpan.FromSeconds(10)),
            ["TouchWithCasAsync"] = c => c.TouchWithCasAsync(DocId, TimeSpan.FromSeconds(10)),
            ["GetAndTouchAsync"] = c => c.GetAndTouchAsync(DocId, TimeSpan.FromSeconds(10)),
            ["GetAndLockAsync"] = c => c.GetAndLockAsync(DocId, TimeSpan.FromSeconds(10)),
            ["GetAnyReplicaAsync"] = c => c.GetAnyReplicaAsync(DocId),
            ["GetAllReplicasAsync"] = c => Task.WhenAll(c.GetAllReplicasAsync(DocId)),
            ["LookupInAsync"] = c => c.LookupInAsync(DocId, LookupSpecs),
            ["LookupInAnyReplicaAsync"] = c => c.LookupInAnyReplicaAsync(DocId, LookupSpecs),
            ["LookupInAllReplicasAsync"] = c => Drain(c.LookupInAllReplicasAsync(DocId, LookupSpecs)),
            ["MutateInAsync"] = c => c.MutateInAsync(DocId, MutateSpecs),
            ["ScanAsync"] = c => Drain(c.ScanAsync(new RangeScan())),
            ["AppendAsync"] = c => c.AppendAsync(DocId, [1, 2, 3]),
            ["PrependAsync"] = c => c.PrependAsync(DocId, [1, 2, 3]),
            ["IncrementAsync"] = c => c.IncrementAsync(DocId),
            ["DecrementAsync"] = c => c.DecrementAsync(DocId)
        };

        public static TheoryData<string> OperationLabels => new(Operations.Keys);

        /// <summary>
        /// ScanAsync cannot be asserted the same way as the rest: PartitionScan builds its own
        /// operations and reads Cid off the collection, so nothing it sends carries the document
        /// key. Its coverage is the collection-level assertion that the ID was resolved at all.
        /// </summary>
        private const string ScanLabel = "ScanAsync";

        [Theory]
        [MemberData(nameof(OperationLabels))]
        public async Task Operation_resolves_the_collection_id_before_dispatch(string label)
        {
            var (collection, bucket) = CreateCollection();

            try
            {
                await Operations[label](collection).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Every operation fails - the fake bucket serves no documents. What it put on the
                // wire before failing is the point.
            }

            Assert.True(bucket.FetchedCid, $"{label} never fetched a collection ID.");
            Assert.Equal(FakeCid, collection.Cid!.Value);

            var keyedOps = bucket.Dispatched.Where(op => op.Key == DocId).ToList();

            // Without this the assertion below passes for an operation that dispatched nothing.
            if (label != ScanLabel)
            {
                Assert.NotEmpty(keyedOps);
            }

            Assert.All(keyedOps, op =>
                Assert.True(op.Cid == FakeCid,
                    $"{label} dispatched {op.OpType} for key '{op.Key}' with Cid {(op.Cid?.ToString() ?? "null")}."));
        }

        /// <summary>
        /// The table above is only as good as its coverage, and a new operation added without an
        /// entry is exactly how NCBC-4285 happened. This fails when the public surface outgrows it.
        /// </summary>
        [Fact]
        public void Every_public_operation_is_covered()
        {
            var surface = typeof(ICouchbaseCollection).GetMethods()
                .Concat(typeof(IBinaryCollection).GetMethods())
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .Distinct()
                .ToList();

            var covered = Operations.Keys
                .Select(label => label.Split('<')[0])
                .ToHashSet();

            var missing = surface.Where(name => !covered.Contains(name)).ToList();

            Assert.True(missing.Count == 0,
                $"Add to the collection ID coverage table: {string.Join(", ", missing)}. Every " +
                "key/value operation has to resolve the collection ID before it dispatches, or the " +
                "server reads the first byte of the document key as the collection ID (NCBC-4285).");
        }

        /// <summary>
        /// The CID is cached on the collection and PopulateCidAsync short-circuits on it, so each
        /// case needs its own collection or every case after the first passes for the wrong reason.
        /// </summary>
        [Fact]
        public void Each_case_starts_without_a_collection_id()
        {
            var (collection, bucket) = CreateCollection();

            Assert.Null(collection.Cid);
            Assert.False(bucket.FetchedCid);
        }

        /// <summary>
        /// A bucket with no collections - a memcached bucket - still talks to a connection that
        /// negotiated collections in HELO, so the server reads a leb128 collection ID from every key.
        /// The default collection, 0, is the right one, and there is nothing to look up. Leaving the
        /// CID null is why every KV operation against a memcached bucket failed with
        /// CollectionNotFound after burning the full KvTimeout.
        /// </summary>
        [Fact]
        public async Task A_bucket_without_collections_uses_the_default_collection_id()
        {
            var (collection, bucket) = CreateCollection(supportsCollections: false, defaultCollection: true);

            await Invoke(() => collection.GetAsync(DocId)).ConfigureAwait(false);

            Assert.False(bucket.FetchedCid);
            Assert.True(collection.Cid.HasValue,
                "A bucket without collections left the CID null, so a collections-negotiated " +
                "connection would read the first byte of the key as the collection ID.");
            Assert.Equal(CouchbaseCollection.DefaultCollectionId, collection.Cid!.Value);
            AssertAllKeyedOpsCarry(bucket, CouchbaseCollection.DefaultCollectionId);
        }

        /// <summary>
        /// Asking for a named collection on a cluster or bucket that has none cannot be satisfied, so
        /// say so at the boundary instead of sending an operation whose collection the server cannot
        /// resolve - which surfaced as CollectionNotFound after the full KvTimeout. Matches the JVM's
        /// BaseKeyValueRequest.encodedExternalKeyWithCollection.
        /// </summary>
        [Fact]
        public async Task A_named_collection_without_collections_support_throws()
        {
            var (collection, bucket) = CreateCollection(supportsCollections: false, defaultCollection: false);

            var exception = await Assert.ThrowsAsync<FeatureNotAvailableException>(
                () => collection.GetAsync(DocId)).ConfigureAwait(false);

            Assert.Contains("s.c", exception.Message);
            Assert.Empty(bucket.Dispatched);
        }

        /// <summary>
        /// The default scope and collection are defined to be collection ID 0, so the first operation
        /// against them should not spend a GET_CID round trip discovering that.
        /// </summary>
        [Fact]
        public async Task The_default_collection_needs_no_lookup()
        {
            var (collection, bucket) = CreateCollection(defaultCollection: true);

            await Invoke(() => collection.GetAsync(DocId)).ConfigureAwait(false);

            Assert.False(bucket.FetchedCid);
            Assert.Equal(CouchbaseCollection.DefaultCollectionId, collection.Cid!.Value);
            AssertAllKeyedOpsCarry(bucket, CouchbaseCollection.DefaultCollectionId);
        }

        /// <summary>
        /// PopulateCidAsync(retryIfFailure: false) is meant to send the GET_CID without going through
        /// the retry orchestrator, but both lazies were built with retryIfFailure: true, so the
        /// no-retry one retried. Only reachable with forceUpdate false, which nothing currently does -
        /// a trap for the next caller rather than a live bug.
        /// </summary>
        [Fact]
        public async Task A_no_retry_collection_id_fetch_does_not_go_through_the_retry_path()
        {
            var (collection, bucket) = CreateCollection();

            await collection.PopulateCidAsync(retryIfFailure: false).ConfigureAwait(false);

            Assert.True(bucket.FetchedCid);
            Assert.False(bucket.FetchedCidViaRetry,
                "PopulateCidAsync(retryIfFailure: false) went through the retry path.");
        }

        [Fact]
        public async Task A_retrying_collection_id_fetch_does_go_through_the_retry_path()
        {
            var (collection, bucket) = CreateCollection();

            await collection.PopulateCidAsync(retryIfFailure: true).ConfigureAwait(false);

            Assert.True(bucket.FetchedCid);
            Assert.True(bucket.FetchedCidViaRetry);
        }

        /// <summary>
        /// GetCid.GetValueAsUint() returns null for a Success response whose body is empty or cannot
        /// be parsed. That null used to be assigned to Cid and carried to the wire; it must fail where
        /// the cause is still known.
        /// </summary>
        [Fact]
        public async Task A_successful_get_cid_with_no_body_fails_at_the_lookup()
        {
            var (collection, bucket) = CreateCollection();
            bucket.ServeEmptyCidBody = true;

            var exception = await Assert.ThrowsAsync<CouchbaseException>(
                () => collection.PopulateCidAsync().AsTask()).ConfigureAwait(false);

            Assert.Contains("s.c", exception.Message);
            Assert.Null(collection.Cid);
        }

        private static async Task Invoke(Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The fake bucket serves no documents. What went on the wire is the point.
            }
        }

        private static void AssertAllKeyedOpsCarry(CidFakeBucket bucket, uint expected)
        {
            var keyedOps = bucket.Dispatched.Where(op => op.Key == DocId).ToList();
            Assert.NotEmpty(keyedOps);
            Assert.All(keyedOps, op => Assert.Equal(expected, op.Cid!.Value));
        }

        private static async Task Drain<T>(IAsyncEnumerable<T> source)
        {
            await foreach (var _ in source.ConfigureAwait(false))
            {
            }
        }

        /// <summary>
        /// A collections-capable fake bucket on its own, for tests that need a real BucketBase and its
        /// ClusterContext rather than a whole collection.
        /// </summary>
        internal static CidFakeBucket CreateBucketWithCollections()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            config.VBucketServerMap.VBucketMap = Enumerable.Repeat<short[]>([1, 0, 2, 3], 1024).ToArray();
            config.BucketCapabilities =
            [
                BucketCapabilities.COLLECTIONS,
                BucketCapabilities.RANGE_SCAN,
                BucketCapabilities.SUBDOC_REPLICA_READ
            ];

            return new CidFakeBucket(config);
        }

        private static (CouchbaseCollection Collection, CidFakeBucket Bucket) CreateCollection(
            bool supportsCollections = true, bool defaultCollection = false)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // Every key maps to the same topology. Unlike the zone-aware fixture, collections are
            // supported here - that is the whole point - and range scans too, so ScanAsync gets
            // past its feature check.
            config.VBucketServerMap.VBucketMap = Enumerable.Repeat<short[]>([1, 0, 2, 3], 1024).ToArray();
            config.BucketCapabilities = supportsCollections
                ?
                [
                    BucketCapabilities.COLLECTIONS,
                    BucketCapabilities.RANGE_SCAN,
                    BucketCapabilities.SUBDOC_REPLICA_READ
                ]
                :
                [
                    BucketCapabilities.RANGE_SCAN,
                    BucketCapabilities.SUBDOC_REPLICA_READ
                ];

            var bucket = new CidFakeBucket(config);

            var collection = new CouchbaseCollection(bucket,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()),
                new Mock<ILogger<CouchbaseCollection>>().Object,
                new Mock<ILogger<GetResult>>().Object,
                new Mock<IRedactor>().Object,
                defaultCollection ? CouchbaseCollection.DefaultCollectionName : "c",
                defaultCollection
                    ? Mock.Of<IScope>(scope => scope.IsDefaultScope == true &&
                                               scope.Name == Scope.DefaultScopeName)
                    : Mock.Of<IScope>(scope => scope.IsDefaultScope == false && scope.Name == "s"),
                new NoopRequestTracer(),
                NullFallbackTypeSerializerProvider.Instance,
                Mock.Of<IServiceProvider>());

            return (collection, bucket);
        }

        internal record DispatchedOp(string OpType, string Key, uint? Cid);

        /// <summary>
        /// Serves GET_CID and nothing else: every other operation is recorded and then failed, so
        /// the collection method unwinds immediately and the test can look at what it sent.
        /// </summary>
        internal class CidFakeBucket : BucketBase
        {
            private readonly List<DispatchedOp> _dispatched = new();
            private readonly object _lock = new();

            public CidFakeBucket(BucketConfig config)
                : base(config.Name,
                    new ClusterContext(null,
                        new ClusterOptions().WithPasswordAuthentication("username", "password")),
                    new Mock<IScopeFactory>().Object,
                    CreateRetryOrchestrator(),
                    new Mock<ILogger>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(), null)
            {
                CurrentConfig = config;
                KeyMapper = new VBucketKeyMapper(config, new VBucketServerMap(config.VBucketServerMap),
                    new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            }

            /// <summary>Whether a GET_CID was sent.</summary>
            public bool FetchedCid { get; private set; }

            /// <summary>
            /// Which path the GET_CID took. PopulateCidAsync(retryIfFailure: false) must reach
            /// SendAsync, not RetryAsync - the observable difference the no-retry lazy exists for.
            /// </summary>
            public bool FetchedCidViaRetry { get; private set; }

            /// <summary>
            /// Answer GET_CID with a Success status and no body, which is what makes
            /// GetCid.GetValueAsUint() return null.
            /// </summary>
            public bool ServeEmptyCidBody { get; set; }

            /// <summary>Snapshot of what was dispatched, GET_CID aside.</summary>
            public IReadOnlyList<DispatchedOp> Dispatched
            {
                get
                {
                    lock (_lock)
                    {
                        return _dispatched.ToList();
                    }
                }
            }

            public virtual Task<ResponseStatus> Dispatch(IOperation operation, bool viaRetry = true)
            {
                if (operation is GetCid getCid)
                {
                    FetchedCid = true;
                    FetchedCidViaRetry = viaRetry;

                    if (ServeEmptyCidBody)
                    {
                        return Task.FromResult(ResponseStatus.Success);
                    }

                    var response = MemoryPool<byte>.Shared.RentAndSlice(GetCidResponse.Length);
                    GetCidResponse.AsMemory().CopyTo(response.Memory);
                    getCid.Read(response);

                    return Task.FromResult(ResponseStatus.Success);
                }

                lock (_lock)
                {
                    _dispatched.Add(new DispatchedOp(operation.GetType().Name, operation.Key, operation.Cid));
                }

                return Task.FromException<ResponseStatus>(
                    new TemporaryFailureException("The fake bucket serves no documents."));
            }

            private static IRetryOrchestrator CreateRetryOrchestrator()
            {
                var mock = new Mock<IRetryOrchestrator>();

                mock.Setup(m => m.RetryAsync(It.IsAny<BucketBase>(), It.IsAny<IOperation>(),
                        It.IsAny<CancellationTokenPair>()))
                    .Returns((BucketBase bucket, IOperation op, CancellationTokenPair _) =>
                        ((CidFakeBucket) bucket).Dispatch(op));

                return mock.Object;
            }

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default) =>
                Dispatch(op, viaRetry: false);

            public override ICouchbaseCollectionManager Collections => throw new NotImplementedException();

#pragma warning disable CS0618, CS0672 // Obsolete View service members
            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument,
                string viewName, ViewOptions options = null) => throw new NotImplementedException();
#pragma warning restore CS0618, CS0672

            public override Task ForceConfigUpdateAsync() => throw new NotImplementedException();

            public override IScope Scope(string scopeName) => throw new NotImplementedException();

            internal override Task BootstrapAsync(IClusterNode bootstrapNodes) => throw new NotImplementedException();

            public override Task ConfigUpdatedAsync(BucketConfig newConfig) => throw new NotImplementedException();
        }
    }
}
