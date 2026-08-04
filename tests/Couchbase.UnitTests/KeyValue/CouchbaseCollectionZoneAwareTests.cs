using System;
using System.Collections.Generic;
using System.Linq;
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
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.KeyValue.ZoneAware;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// Zone-aware replica reads (RFC-0078): which nodes are read, and where an unusable server
    /// group fails. The fixture config splits four nodes into group_1 (indexes 0 and 1) and
    /// group_2 (indexes 2 and 3).
    /// </summary>
    public class CouchbaseCollectionZoneAwareTests
    {
        private const string PreferredGroup = "group_1";
        private const string DocId = "thekey";

        // group_1 holds the primary (node 1) and one replica (node 0).
        private static readonly short[] GroupHoldsCopy = [1, 0, 2, 3];

        // Only group_2 nodes hold a copy.
        private static readonly short[] GroupHoldsNoCopy = [2, 3];

        private static readonly LookupInSpec[] Specs = [LookupInSpec.Get("name")];

        #region No preferred server group in the ClusterOptions

        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public async Task GetAnyReplica_Without_PreferredServerGroup_Throws(InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null, GroupHoldsCopy);

            await Assert.ThrowsAsync<DocumentUnretrievableException>(() => collection.GetAnyReplicaAsync(DocId,
                new GetAnyReplicaOptions().ReadPreference(readPreference)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public void GetAllReplicas_Without_PreferredServerGroup_Throws(InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null, GroupHoldsCopy);

            Assert.Throws<DocumentUnretrievableException>(() => collection.GetAllReplicasAsync(DocId,
                new GetAllReplicasOptions().ReadPreference(readPreference)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public async Task LookupInAnyReplica_Without_PreferredServerGroup_Throws(InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null, GroupHoldsCopy);

            await Assert.ThrowsAsync<DocumentUnretrievableException>(() => collection.LookupInAnyReplicaAsync(DocId, Specs,
                new LookupInAnyReplicaOptions().ReadPreference(readPreference)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        // No read preference works around an unset group, so both of these must fail at the call
        // site rather than on the stream.
        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public void LookupInAllReplicas_Without_PreferredServerGroup_Throws_Before_Enumeration(
            InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null, GroupHoldsCopy);

            Assert.Throws<DocumentUnretrievableException>(() => collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().ReadPreference(readPreference)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        #endregion

        #region Preferred server group holds no copy of the document

        [Fact]
        public void GetAllReplicas_SelectedServerGroup_Without_Local_Copy_Throws()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsNoCopy);

            Assert.Throws<DocumentUnretrievableException>(() => collection.GetAllReplicasAsync(DocId,
                new GetAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroup)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public void LookupInAllReplicas_SelectedServerGroup_Without_Local_Copy_Throws_Before_Enumeration()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsNoCopy);

            Assert.Throws<DocumentUnretrievableException>(() => collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroup)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public async Task GetAllReplicas_Fallback_Without_Local_Copy_Reads_Every_Copy()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsNoCopy);

            await ObserveAsync(collection.GetAllReplicasAsync(DocId,
                new GetAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroupWithFallback)));

            // Primary node 2 and replica node 3, neither of which is in group_1.
            Assert.Equal([null, (short) 3], bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public async Task LookupInAllReplicas_Fallback_Without_Local_Copy_Reads_Every_Copy()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsNoCopy);

            await ObserveAsync(collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroupWithFallback)));

            Assert.Equal([null, (short) 3], bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public async Task LookupInAllReplicas_Fallback_With_Unknown_Group_Name_Reads_Every_Copy()
        {
            var (collection, bucket) = CreateCollection("group_does_not_exist", GroupHoldsCopy);

            await ObserveAsync(collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroupWithFallback)));

            Assert.Equal([null, (short) 0, (short) 2, (short) 3], bucket.DispatchedReplicaIndexes);
        }

        #endregion

        #region Preferred server group holds a copy of the document

        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public async Task GetAllReplicas_Reads_Only_The_Preferred_Group(InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsCopy);

            await ObserveAsync(collection.GetAllReplicasAsync(DocId,
                new GetAllReplicasOptions().ReadPreference(readPreference)));

            // Replica node 0 then primary node 1, both in group_1. Nodes 2 and 3 are not read.
            Assert.Equal([(short) 0, null], bucket.DispatchedReplicaIndexes);
        }

        [Theory]
        [InlineData(InternalReadPreference.SelectedServerGroup)]
        [InlineData(InternalReadPreference.SelectedServerGroupWithFallback)]
        public async Task LookupInAllReplicas_Reads_Only_The_Preferred_Group(InternalReadPreference readPreference)
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsCopy);

            await ObserveAsync(collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().ReadPreference(readPreference)));

            Assert.Equal([(short) 0, null], bucket.DispatchedReplicaIndexes);
        }

        #endregion

        #region No read preference

        [Fact]
        public async Task GetAllReplicas_Without_ReadPreference_Reads_Every_Copy()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsCopy);

            await ObserveAsync(collection.GetAllReplicasAsync(DocId));

            Assert.Equal([null, (short) 0, (short) 2, (short) 3], bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public async Task LookupInAllReplicas_Without_ReadPreference_Reads_Every_Copy()
        {
            var (collection, bucket) = CreateCollection(PreferredGroup, GroupHoldsCopy);

            await ObserveAsync(collection.LookupInAllReplicasAsync(DocId, Specs));

            Assert.Equal([null, (short) 0, (short) 2, (short) 3], bucket.DispatchedReplicaIndexes);
        }

        #endregion

        /// <summary>
        /// Every read the fake bucket receives fails, so drain the results to record what was sent.
        /// </summary>
        private static async Task ObserveAsync(IEnumerable<Task<IGetReplicaResult>> tasks)
        {
            foreach (var task in tasks)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => task);
            }
        }

        private static async Task ObserveAsync(IAsyncEnumerable<ILookupInReplicaResult> results)
        {
            await foreach (var result in results)
            {
                Assert.Fail($"Every read was expected to fail, but the stream yielded {result}");
            }
        }

        private static (CouchbaseCollection Collection, ZoneAwareFakeBucket Bucket) CreateCollection(
            string preferredServerGroup, short[] vBucketMapRow)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // Every key maps to the same topology, and collections are left unsupported so that no
            // collection ID is fetched.
            config.VBucketServerMap.VBucketMap = Enumerable.Repeat(vBucketMapRow, 1024).ToArray();
            config.BucketCapabilities = [BucketCapabilities.SUBDOC_REPLICA_READ];

            var bucket = new ZoneAwareFakeBucket(config, preferredServerGroup);

            var collection = new CouchbaseCollection(bucket,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()),
                new Mock<ILogger<CouchbaseCollection>>().Object,
                new Mock<ILogger<GetResult>>().Object,
                new Mock<IRedactor>().Object,
                CouchbaseCollection.DefaultCollectionName,
                Mock.Of<IScope>(),
                new NoopRequestTracer(),
                NullFallbackTypeSerializerProvider.Instance,
                Mock.Of<IServiceProvider>());

            return (collection, bucket);
        }

        /// <summary>
        /// Records the node each operation was routed to, then fails it. What is read matters here,
        /// not what comes back.
        /// </summary>
        internal class ZoneAwareFakeBucket : BucketBase
        {
            public ZoneAwareFakeBucket(BucketConfig config, string preferredServerGroup)
                : base(config.Name,
                    new ClusterContext(null, new ClusterOptions
                        {
                            PreferredServerGroup = preferredServerGroup
                        }.WithPasswordAuthentication("username", "password")),
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

            /// <summary>
            /// The node index each read was sent to, in dispatch order. Null is the primary.
            /// </summary>
            public List<short?> DispatchedReplicaIndexes { get; } = new();

            public virtual Task<ResponseStatus> RecordAndFail(IOperation operation)
            {
                DispatchedReplicaIndexes.Add(operation.ReplicaIdx);
                return Task.FromException<ResponseStatus>(
                    new TemporaryFailureException("The fake bucket serves no documents."));
            }

            private static IRetryOrchestrator CreateRetryOrchestrator()
            {
                var mock = new Mock<IRetryOrchestrator>();

                mock.Setup(m => m.RetryAsync(It.IsAny<BucketBase>(), It.IsAny<IOperation>(),
                        It.IsAny<CancellationTokenPair>()))
                    .Returns((BucketBase bucket, IOperation op, CancellationTokenPair _) =>
                        ((ZoneAwareFakeBucket) bucket).RecordAndFail(op));

                return mock.Object;
            }

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default) =>
                RecordAndFail(op);

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
