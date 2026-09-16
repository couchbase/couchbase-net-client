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
    public class CouchbaseCollectionGetReplicaTests
    {
        private const string DocId = "thekey";

        [Fact]
        public async Task GetReplica_Sends_One_ReplicaRead_Carrying_The_Strategy()
        {
            var (collection, bucket) = CreateCollection();
            var strategy = GetReplicaStrategy.FromIndex(ReplicaIndex.Second);

            await Assert.ThrowsAnyAsync<Exception>(() => collection.GetReplicaAsync(DocId, strategy));

            var op = Assert.Single(bucket.DispatchedOperations);
            var replicaRead = Assert.IsType<ReplicaRead<byte[]>>(op);
            Assert.Same(strategy, replicaRead.ReplicaStrategy);
            Assert.Null(replicaRead.ReplicaIdx);
            Assert.Equal(DocId, replicaRead.Key);
            Assert.Equal(OpCode.ReplicaRead, replicaRead.OpCode);
        }

        [Fact]
        public async Task GetReplica_Without_Strategy_Throws()
        {
            var (collection, bucket) = CreateCollection();

            await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetReplicaAsync(DocId, null));
            Assert.Empty(bucket.DispatchedOperations);
        }

        [Fact]
        public async Task GetReplica_Surfaces_DocumentNotFoundOnReplica_Unwrapped()
        {
            var (collection, _) = CreateCollection(
                new DocumentNotFoundOnReplicaException("not on that replica"));

            var exception = await Assert.ThrowsAsync<DocumentNotFoundOnReplicaException>(() =>
                collection.GetReplicaAsync(DocId, GetReplicaStrategy.FromIndex(ReplicaIndex.First)));

            Assert.Equal("not on that replica", exception.Message);
        }

        [Fact]
        public void DocumentNotFoundOnReplica_Is_A_DocumentNotFound()
        {
            Assert.True(typeof(DocumentNotFoundException).IsAssignableFrom(typeof(DocumentNotFoundOnReplicaException)));
        }

        #region GetReplicaOptions

        [Fact]
        public void Options_Timeout_Leaves_Default_Untouched()
        {
            var options = GetReplicaOptions.Default.Timeout(TimeSpan.FromSeconds(1));

            Assert.NotSame(GetReplicaOptions.Default, options);
            Assert.Equal(TimeSpan.FromSeconds(1), options.TimeoutValue);
            Assert.Null(GetReplicaOptions.Default.TimeoutValue);
        }

        [Fact]
        public void Options_CancellationToken_Leaves_Default_Untouched()
        {
            using var cts = new CancellationTokenSource();
            var options = GetReplicaOptions.Default.CancellationToken(cts.Token);

            Assert.NotSame(GetReplicaOptions.Default, options);
            Assert.Equal(cts.Token, options.TokenValue);
            Assert.Equal(default, GetReplicaOptions.Default.TokenValue);
        }

        [Fact]
        public void Options_Transcoder_Leaves_Default_Untouched()
        {
            var transcoder = new LegacyTranscoder();
            var options = GetReplicaOptions.Default.Transcoder(transcoder);

            Assert.NotSame(GetReplicaOptions.Default, options);
            Assert.Same(transcoder, options.TranscoderValue);
            Assert.Null(GetReplicaOptions.Default.TranscoderValue);
        }

        [Fact]
        public void Options_AsReadOnly_Round_Trips()
        {
            using var cts = new CancellationTokenSource();
            var transcoder = new LegacyTranscoder();
            var retryStrategy = new BestEffortRetryStrategy();
            var span = NoopRequestTracer.Instance.RequestSpan("test");

            var readOnly = new GetReplicaOptions()
                .Timeout(TimeSpan.FromSeconds(3))
                .CancellationToken(cts.Token)
                .Transcoder(transcoder)
                .RetryStrategy(retryStrategy)
                .RequestSpan(span)
                .AsReadOnly();

            Assert.Equal(TimeSpan.FromSeconds(3), readOnly.Timeout);
            Assert.Equal(cts.Token, readOnly.Token);
            Assert.Same(transcoder, readOnly.Transcoder);
            Assert.Same(retryStrategy, readOnly.RetryStrategy);
            Assert.Same(span, readOnly.RequestSpan);
        }

        [Fact]
        public void Options_Default_AsReadOnly_Is_Empty()
        {
            var readOnly = GetReplicaOptions.Default.AsReadOnly();

            Assert.Null(readOnly.Timeout);
            Assert.Equal(default, readOnly.Token);
            Assert.Null(readOnly.Transcoder);
            Assert.Null(readOnly.RetryStrategy);
            Assert.Null(readOnly.RequestSpan);
        }

        #endregion

        private static (CouchbaseCollection Collection, GetReplicaFakeBucket Bucket) CreateCollection(
            Exception failWith = null)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // No COLLECTIONS capability, so no collection id lookup happens.
            config.VBucketServerMap.VBucketMap = Enumerable.Repeat(new short[] { 1, 0, 2, 3 }, 1024).ToArray();
            config.BucketCapabilities = [BucketCapabilities.SUBDOC_REPLICA_READ];

            var bucket = new GetReplicaFakeBucket(config, failWith);

            var collection = new CouchbaseCollection(bucket,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()),
                new Mock<ILogger<CouchbaseCollection>>().Object,
                new Mock<ILogger<GetResult>>().Object,
                new Mock<IRedactor>().Object,
                CouchbaseCollection.DefaultCollectionName,
                Mock.Of<IScope>(scope => scope.IsDefaultScope == true && scope.Name == Scope.DefaultScopeName),
                new NoopRequestTracer(),
                NullFallbackTypeSerializerProvider.Instance,
                Mock.Of<IServiceProvider>());

            return (collection, bucket);
        }

        internal class GetReplicaFakeBucket : BucketBase
        {
            private readonly Exception _failWith;

            public GetReplicaFakeBucket(BucketConfig config, Exception failWith)
                : base(config.Name,
                    new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
                    new Mock<IScopeFactory>().Object,
                    CreateRetryOrchestrator(),
                    new Mock<ILogger>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(), null)
            {
                _failWith = failWith ?? new TemporaryFailureException("The fake bucket serves no documents.");
                CurrentConfig = config;
                KeyMapper = new VBucketKeyMapper(config, new VBucketServerMap(config.VBucketServerMap),
                    new VBucketFactory(new Mock<ILogger<VBucket>>().Object));
            }

            public List<IOperation> DispatchedOperations { get; } = new();

            public Task<ResponseStatus> RecordAndFail(IOperation operation)
            {
                DispatchedOperations.Add(operation);
                return Task.FromException<ResponseStatus>(_failWith);
            }

            private static IRetryOrchestrator CreateRetryOrchestrator()
            {
                var mock = new Mock<IRetryOrchestrator>();

                mock.Setup(m => m.RetryAsync(It.IsAny<BucketBase>(), It.IsAny<IOperation>(),
                        It.IsAny<CancellationTokenPair>()))
                    .Returns((BucketBase bucket, IOperation op, CancellationTokenPair _) =>
                        ((GetReplicaFakeBucket) bucket).RecordAndFail(op));

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
