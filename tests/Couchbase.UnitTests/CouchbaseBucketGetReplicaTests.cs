using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class CouchbaseBucketGetReplicaTests
    {
        private const string Key = "key";

        [Fact]
        public async Task Second_Replica_Goes_To_Its_Node()
        {
            var bucket = await CreateBucketAsync([1, 0, 2, 3], numReplicas: 3);

            await bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.Second));

            Assert.Equal([2], bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task First_Replica_Goes_To_Its_Node()
        {
            var bucket = await CreateBucketAsync([1, 0, 2, 3], numReplicas: 3);

            await bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.First));

            Assert.Equal([0], bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task Unavailable_Replica_Without_Wrap_Throws_And_Sends_Nothing()
        {
            var bucket = await CreateBucketAsync([1, 0, -1, 3], numReplicas: 3);

            var exception = await Assert.ThrowsAsync<ReplicaIndexCurrentlyUnavailableException>(() =>
                bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.Second)));

            var context = Assert.IsType<KeyValueErrorContext>(exception.Context);
            Assert.Equal(Key, context.DocumentKey);
            Assert.Equal("default", context.BucketName);
            Assert.Equal(OpCode.ReplicaRead, context.OpCode);
            Assert.Empty(bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task Replica_On_A_Node_Outside_The_Server_List_Throws_And_Sends_Nothing()
        {
            var bucket = await CreateBucketAsync([1, 9], numReplicas: 1);

            await Assert.ThrowsAsync<ReplicaIndexCurrentlyUnavailableException>(() =>
                bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.First)));
            Assert.Empty(bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task Unavailable_Replica_With_Wrap_Goes_To_The_Next_Node()
        {
            var bucket = await CreateBucketAsync([1, 0, -1, 3], numReplicas: 3);

            await bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.Second,
                new GetReplicaStrategyFromIndexOptions().Wrap()));

            Assert.Equal([3], bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task Bucket_Without_Replicas_Throws_And_Sends_Nothing()
        {
            var bucket = await CreateBucketAsync([1, 0], numReplicas: 0);

            await Assert.ThrowsAsync<ReplicaIndexOutOfBoundsException>(() =>
                bucket.SendAsync(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.First)));

            Assert.Empty(bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task Replica_Is_Reselected_On_Every_Dispatch()
        {
            var bucket = await CreateBucketAsync([1, 0, 2, 3], numReplicas: 3);
            var strategy = GetReplicaStrategy.FromIndex(ReplicaIndex.Second);
            using var op = new ReplicaRead<byte[]>(Key, strategy);

            await bucket.SendAsync(op, default);
            await bucket.ApplyVBucketMapAsync([1, 0, 3, 2], numReplicas: 3);
            await bucket.SendAsync(op, default);

            Assert.Equal([2, 3], bucket.ServersThatReceivedOps);
        }

        [Fact]
        public async Task A_Grown_Chain_Satisfies_A_Previously_Out_Of_Bounds_Index()
        {
            var bucket = await CreateBucketAsync([1, 0], numReplicas: 1);
            using var op = new ReplicaRead<byte[]>(Key, GetReplicaStrategy.FromIndex(ReplicaIndex.Second));

            await Assert.ThrowsAsync<ReplicaIndexOutOfBoundsException>(() => bucket.SendAsync(op, default));
            Assert.Empty(bucket.ServersThatReceivedOps);

            await bucket.ApplyVBucketMapAsync([1, 0, 2, 3], numReplicas: 3);

            await bucket.SendAsync(op, default);
            Assert.Equal([2], bucket.ServersThatReceivedOps);
        }

        private static async Task<TestBucket> CreateBucketAsync(short[] vBucketMapRow, int numReplicas)
        {
            var bucket = new TestBucket();
            await bucket.ApplyVBucketMapAsync(vBucketMapRow, numReplicas);
            return bucket;
        }

        // Real bucket and key mapper, fake nodes that record what they receive.
        internal sealed class TestBucket
        {
            private readonly CouchbaseBucket _bucket;
            private readonly List<int> _serversThatReceivedOps = new();
            private ulong _rev = 1;

            public TestBucket()
            {
                var keyMapperFactory = new Mock<IVBucketKeyMapperFactory>();
                keyMapperFactory
                    .Setup(x => x.Create(It.IsAny<BucketConfig>(), It.IsAny<CancellationToken>()))
                    .Returns((BucketConfig config, CancellationToken _) => new VBucketKeyMapper(config,
                        new VBucketServerMap(config.VBucketServerMap),
                        new VBucketFactory(new Mock<ILogger<VBucket>>().Object)));

                _bucket = new CouchbaseBucket("default",
                    new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
                    new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object,
                    keyMapperFactory.Object,
                    new Mock<ILogger<CouchbaseBucket>>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(),
                    LoadConfig([0, 0, 0, 0], 3, _rev),
                    new Mock<IConfigPushHandlerFactory>().Object);
            }

            public IReadOnlyList<int> ServersThatReceivedOps => _serversThatReceivedOps;

            // A config push is the only way to install a new key mapper.
            public async Task ApplyVBucketMapAsync(short[] vBucketMapRow, int numReplicas)
            {
                var config = LoadConfig(vBucketMapRow, numReplicas, ++_rev);
                await _bucket.ConfigUpdatedAsync(config);
                Assert.NotNull(_bucket.KeyMapper);

                _bucket.Nodes.Clear();
                var endPoints = new VBucketServerMap(config.VBucketServerMap).EndPoints;
                for (var i = 0; i < endPoints.Count; i++)
                {
                    _bucket.Nodes.Add(CreateNode(endPoints[i], i));
                }
            }

            public Task<ResponseStatus> SendAsync(string key, GetReplicaStrategy strategy)
            {
                using var op = new ReplicaRead<byte[]>(key, strategy);
                return SendAsync(op, default);
            }

            public Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair tokenPair) =>
                _bucket.SendAsync(op, tokenPair);

            private IClusterNode CreateNode(HostEndpointWithPort endPoint, int serverIndex)
            {
                var node = new Mock<IClusterNode>();
                node.SetupGet(x => x.EndPoint).Returns(endPoint);
                node.SetupGet(x => x.KeyEndPoints).Returns(new[] { endPoint });
                node.SetupGet(x => x.HasKv).Returns(true);
                node.Setup(x => x.SendAsync(It.IsAny<IOperation>(), It.IsAny<CancellationTokenPair>()))
                    .Returns(() =>
                    {
                        _serversThatReceivedOps.Add(serverIndex);
                        return Task.FromResult(ResponseStatus.Success);
                    });

                return node.Object;
            }

            private static BucketConfig LoadConfig(short[] vBucketMapRow, int numReplicas, ulong rev)
            {
                var config = ResourceHelper.ReadResource(
                    @"Documents\Configs\configWithReplicasAndServerGroups.json",
                    InternalSerializationContext.Default.BucketConfig);

                config.VBucketServerMap.VBucketMap = Enumerable.Repeat(vBucketMapRow, 1024).ToArray();
                config.VBucketServerMap.NumReplicas = numReplicas;
                config.Rev = rev;
                config.OnDeserialized();

                return config;
            }
        }
    }
}
